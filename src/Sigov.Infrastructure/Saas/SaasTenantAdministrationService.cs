using System.Data;
using System.Text.Json;
using Dapper;
using Sigov.Application.Saas.Modules;
using Sigov.Application.Saas.SuperAdmin;
using Sigov.Infrastructure.Persistence.Dapper;

namespace Sigov.Infrastructure.Saas;

public sealed class SaasTenantAdministrationService(
    DapperContext context,
    IModuleCatalogService catalog) : ISaasTenantAdministrationService
{
    public async Task<SaasTenantListPage> ListAsync(SaasTenantListFilter filter, CancellationToken cancellationToken = default)
    {
        var page = filter.Page < 1 ? 1 : filter.Page;
        var pageSize = filter.PageSize is < 1 or > 100 ? 20 : filter.PageSize;
        var args = new
        {
            Search = string.IsNullOrWhiteSpace(filter.Search) ? null : $"%{filter.Search.Trim()}%",
            Status = NullIfBlank(filter.Status),
            Esfera = NullIfBlank(filter.Esfera),
            Offset = (page - 1) * pageSize,
            Limit = pageSize
        };
        using var connection = context.CreateConnection();
        using var grid = await connection.QueryMultipleAsync(new CommandDefinition(ListSql, args, cancellationToken: cancellationToken)).ConfigureAwait(false);
        var total = await grid.ReadSingleAsync<int>().ConfigureAwait(false);
        var items = (await grid.ReadAsync<SaasTenantListItem>().ConfigureAwait(false)).AsList();
        return new SaasTenantListPage(items, page, pageSize, total);
    }

    public async Task<SaasTenantDetail?> GetAsync(long tenantId, CancellationToken cancellationToken = default)
    {
        using var connection = context.CreateConnection();
        var header = await connection.QuerySingleOrDefaultAsync<TenantHeader>(new CommandDefinition(HeaderSql, new { TenantId = tenantId }, cancellationToken: cancellationToken)).ConfigureAwait(false);
        if (header is null)
            return null;

        var entities = (await connection.QueryAsync<SaasTenantEntityItem>(new CommandDefinition(EntitiesSql, new { TenantId = tenantId }, cancellationToken: cancellationToken)).ConfigureAwait(false)).AsList();
        var users = (await connection.QueryAsync<UserRow>(new CommandDefinition(UsersSql, new { TenantId = tenantId }, cancellationToken: cancellationToken)).ConfigureAwait(false)).AsList();
        var contracts = (await connection.QueryAsync<SaasTenantContractItem>(new CommandDefinition(ContractsSql, new { TenantId = tenantId }, cancellationToken: cancellationToken)).ConfigureAwait(false)).AsList();
        var catalogItems = await catalog.GetModulesAsync(cancellationToken).ConfigureAwait(false);
        return new SaasTenantDetail(
            header.Id,
            header.Name,
            header.Status,
            header.Esfera,
            header.Plan,
            header.LastActivityUtc,
            entities,
            users.Select(user => new SaasTenantUserItem(user.Id, user.Name, user.Email, user.Active, SplitProfiles(user.Profiles))).ToArray(),
            contracts,
            catalogItems.Select(item => new ModuleCatalogOption(item.Codigo, item.Nome, item.Dependencias)).ToArray());
    }

    public Task<SaasModuleContractResult> ContractAsync(SaasModuleContractCommand command, long userId, string correlationId, CancellationToken cancellationToken = default) =>
        MutateAsync(command, userId, correlationId, "CONTRATADO", "SAAS_MODULO_CONTRATAR", cancellationToken);

    public Task<SaasModuleContractResult> SuspendAsync(SaasModuleContractCommand command, long userId, string correlationId, CancellationToken cancellationToken = default) =>
        MutateAsync(command, userId, correlationId, "SUSPENSO", "SAAS_MODULO_SUSPENDER", cancellationToken);

    public Task<SaasModuleContractResult> ReactivateAsync(SaasModuleContractCommand command, long userId, string correlationId, CancellationToken cancellationToken = default) =>
        MutateAsync(command, userId, correlationId, "HABILITADO", "SAAS_MODULO_REATIVAR", cancellationToken);

    private async Task<SaasModuleContractResult> MutateAsync(
        SaasModuleContractCommand command,
        long userId,
        string correlationId,
        string targetStatus,
        string auditAction,
        CancellationToken cancellationToken)
    {
        if (command.TenantId <= 0 || string.IsNullOrWhiteSpace(command.ModuleCode))
            return new(false, "Tenant e módulo são obrigatórios.");
        if (string.IsNullOrWhiteSpace(command.Justification) || command.Justification.Trim().Length < 8)
            return new(false, "Justificativa obrigatória com no mínimo 8 caracteres.");
        if (userId <= 0)
            return new(false, "Usuário autenticado é obrigatório.");

        var module = await catalog.FindByCodeAsync(command.ModuleCode, cancellationToken).ConfigureAwait(false);
        if (module is null)
            return new(false, "Módulo inexistente no catálogo modulo_saas.");

        using var connection = context.CreateConnection();
        connection.Open();
        using var transaction = connection.BeginTransaction();
        try
        {
            var tenantStatus = await connection.ExecuteScalarAsync<string?>(new CommandDefinition(
                "select upper(status) from sigov.tenant where id=@TenantId and ativo and not is_deleted",
                new { command.TenantId }, transaction, cancellationToken: cancellationToken)).ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(tenantStatus))
                return Rollback(transaction, "Tenant inexistente ou inativo.");
            if (tenantStatus is "SUSPENSO" or "CANCELADO" or "EXCLUIDO")
                return Rollback(transaction, "Tenant suspenso ou cancelado não admite alteração de contrato.");

            if (string.Equals(targetStatus, "CONTRATADO", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(targetStatus, "HABILITADO", StringComparison.OrdinalIgnoreCase))
            {
                foreach (var dependency in module.Dependencias)
                {
                    var existing = await connection.ExecuteScalarAsync<bool>(new CommandDefinition(
                        """
                        select exists(
                          select 1 from sigov.tenant_modulo_contratado
                          where tenant_id=@TenantId and modulo_codigo=@Codigo and ativo
                            and status in ('CONTRATADO','HABILITADO','ATIVO','TRIAL','EM_IMPLANTACAO','BETA')
                            and (vigencia_inicio is null or vigencia_inicio<=current_date)
                            and (vigencia_fim is null or vigencia_fim>=current_date))
                        """,
                        new { command.TenantId, Codigo = dependency }, transaction, cancellationToken: cancellationToken)).ConfigureAwait(false);
                    if (!existing)
                        return Rollback(transaction, $"Dependência contratual não atendida: {dependency}.");
                }
            }

            var before = await connection.QuerySingleOrDefaultAsync<ContractRow>(new CommandDefinition(
                CurrentSql, new { command.TenantId, command.ModuleCode }, transaction, cancellationToken: cancellationToken)).ConfigureAwait(false);
            if (command.ExpectedUpdatedAt.HasValue && before is not null &&
                before.UpdatedAt != command.ExpectedUpdatedAt && before.CreatedAt != command.ExpectedUpdatedAt)
                return Rollback(transaction, "O contrato foi alterado por outro usuário. Recarregue e tente novamente.");

            var correlation = Guid.TryParse(correlationId, out var parsed) ? parsed : Guid.NewGuid();
            var from = command.EffectiveFrom ?? DateOnly.FromDateTime(DateTime.UtcNow);
            var rows = await connection.ExecuteAsync(new CommandDefinition(
                UpsertSql,
                new
                {
                    command.TenantId,
                    command.ModuleCode,
                    Status = targetStatus,
                    VigenciaInicio = from,
                    command.EffectiveUntil,
                    Motivo = command.Justification.Trim(),
                    UserId = userId,
                    CorrelationId = correlation,
                    Ativo = !string.Equals(targetStatus, "CANCELADO", StringComparison.OrdinalIgnoreCase)
                }, transaction, cancellationToken: cancellationToken)).ConfigureAwait(false);
            if (rows <= 0)
                return Rollback(transaction, "Não foi possível persistir o contrato.");

            var after = await connection.QuerySingleAsync<SaasTenantContractItem>(new CommandDefinition(
                ContractsSql + " and tm.modulo_codigo=@ModuleCode",
                new { command.TenantId, command.ModuleCode }, transaction, cancellationToken: cancellationToken)).ConfigureAwait(false);

            await connection.ExecuteAsync(new CommandDefinition(
                AuditSql,
                new
                {
                    command.TenantId,
                    UserId = userId,
                    Acao = auditAction,
                    EntidadeId = command.ModuleCode,
                    CorrelationId = correlation,
                    Antes = JsonSerializer.Serialize(before),
                    Depois = JsonSerializer.Serialize(new { after.Status, command.Justification, after.EffectiveFrom, after.EffectiveUntil })
                }, transaction, cancellationToken: cancellationToken)).ConfigureAwait(false);

            transaction.Commit();
            return new(true, "Contrato atualizado.", after);
        }
        catch (Exception)
        {
            transaction.Rollback();
            throw;
        }
    }

    private static SaasModuleContractResult Rollback(IDbTransaction transaction, string message)
    {
        transaction.Rollback();
        return new(false, message);
    }

    private static string? NullIfBlank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private static IReadOnlyList<string> SplitProfiles(string? value) =>
        string.IsNullOrWhiteSpace(value) ? Array.Empty<string>() : value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    private sealed record TenantHeader(long Id, string Name, string Status, string Esfera, string? Plan, DateTimeOffset? LastActivityUtc);
    private sealed record UserRow(long Id, string Name, string Email, bool Active, string? Profiles);
    private sealed record ContractRow(long Id, string Status, DateTimeOffset CreatedAt, DateTimeOffset? UpdatedAt);

    private const string ListSql = """
select count(*)::int
from sigov.tenant t
left join lateral (
  select string_agg(distinct lower(coalesce(sc.esfera_governo, e.esfera_governo)), ', ') as esfera
  from sigov.tenant_entidade te
  left join sigov.entidade e on e.id=te.entidade_id
  left join sigov.saas_cliente sc on sc.tenant_id=t.id and sc.entidade_id=te.entidade_id and sc.ativo and not sc.is_deleted
  where te.tenant_id=t.id and te.ativo
) esfera on true
where t.ativo and not t.is_deleted
  and (@Status is null or upper(t.status)=upper(@Status))
  and (@Search is null or coalesce(t.nome_fantasia,t.nome,t.slug) ilike @Search)
  and (@Esfera is null or esfera.esfera ilike '%'||@Esfera||'%');

select t.id as Id, coalesce(t.nome_fantasia,t.nome,t.slug,'Tenant '||t.id::text) as Name,
       upper(coalesce(t.status,'INATIVO')) as Status,
       coalesce(esfera.esfera, 'nao_informada') as Esfera,
       coalesce(ctx.entities,0) as Entities,
       coalesce(users.active_users,0) as ActiveUsers,
       coalesce(mods.modules,0) as Modules,
       activity.last_activity as LastActivityUtc,
       p.nome as Plan
from sigov.tenant t
left join sigov.tenant_assinatura a on a.tenant_id=t.id and a.ativo and not a.is_deleted
left join sigov.plano_saas p on p.id=a.plano_saas_id
left join lateral (
  select string_agg(distinct lower(coalesce(sc.esfera_governo, e.esfera_governo)), ', ') as esfera
  from sigov.tenant_entidade te
  left join sigov.entidade e on e.id=te.entidade_id
  left join sigov.saas_cliente sc on sc.tenant_id=t.id and sc.entidade_id=te.entidade_id and sc.ativo and not sc.is_deleted
  where te.tenant_id=t.id and te.ativo
) esfera on true
left join lateral (
  select count(*)::int as entities from sigov.tenant_entidade te where te.tenant_id=t.id and te.ativo
) ctx on true
left join lateral (
  select count(*)::int as active_users from sigov.usuario u where u.tenant_id=t.id and u.ativo and not u.is_deleted
) users on true
left join lateral (
  select count(*)::int as modules from sigov.tenant_modulo_contratado tm
  where tm.tenant_id=t.id and tm.ativo and tm.status in ('CONTRATADO','HABILITADO','ATIVO','TRIAL','EM_IMPLANTACAO','BETA')
) mods on true
left join lateral (
  select max(coalesce(s.ultimo_acesso_at, u.ultimo_login_at)) as last_activity
  from sigov.usuario u
  left join sigov.identidade_sessao s on s.usuario_id=u.id
  where u.tenant_id=t.id
) activity on true
where t.ativo and not t.is_deleted
  and (@Status is null or upper(t.status)=upper(@Status))
  and (@Search is null or coalesce(t.nome_fantasia,t.nome,t.slug) ilike @Search)
  and (@Esfera is null or esfera.esfera ilike '%'||@Esfera||'%')
order by t.id
offset @Offset limit @Limit
""";

    private const string HeaderSql = """
select t.id as Id, coalesce(t.nome_fantasia,t.nome,t.slug,'Tenant '||t.id::text) as Name,
       upper(coalesce(t.status,'INATIVO')) as Status,
       coalesce((
         select string_agg(distinct lower(coalesce(sc.esfera_governo, e.esfera_governo)), ', ')
         from sigov.tenant_entidade te
         left join sigov.entidade e on e.id=te.entidade_id
         left join sigov.saas_cliente sc on sc.tenant_id=t.id and sc.entidade_id=te.entidade_id and sc.ativo and not sc.is_deleted
         where te.tenant_id=t.id and te.ativo
       ), 'nao_informada') as Esfera,
       p.nome as Plan,
       (select max(coalesce(s.ultimo_acesso_at, u.ultimo_login_at)) from sigov.usuario u left join sigov.identidade_sessao s on s.usuario_id=u.id where u.tenant_id=t.id) as LastActivityUtc
from sigov.tenant t
left join sigov.tenant_assinatura a on a.tenant_id=t.id and a.ativo and not a.is_deleted
left join sigov.plano_saas p on p.id=a.plano_saas_id
where t.id=@TenantId and t.ativo and not t.is_deleted
""";

    private const string EntitiesSql = """
select e.id as Id, coalesce(e.nome, 'Entidade '||e.id::text) as Name,
       e.tipo_entidade as Type, lower(coalesce(e.esfera_governo,'nao_informada')) as Esfera
from sigov.tenant_entidade te
join sigov.entidade e on e.id=te.entidade_id
where te.tenant_id=@TenantId and te.ativo
order by e.nome
""";

    private const string UsersSql = """
select u.id as Id, coalesce(u.nome, u.login) as Name, u.email as Email, (u.ativo and not u.is_deleted) as Active,
       string_agg(distinct coalesce(pa.codigo_externo, pa.nome), ',') as Profiles
from sigov.usuario u
left join sigov.usuario_grupo ug on ug.usuario_id=u.id and ug.ativo and not ug.is_deleted
left join sigov.grupo_perfil gp on gp.grupo_acesso_id=ug.grupo_acesso_id and gp.ativo and not gp.is_deleted
left join sigov.perfil_acesso pa on pa.id=gp.perfil_acesso_id and pa.ativo and not pa.is_deleted
where u.tenant_id=@TenantId
group by u.id, u.nome, u.email, u.ativo, u.is_deleted
order by u.nome
""";

    private const string ContractsSql = """
select tm.id as Id, tm.modulo_codigo as ModuleCode, coalesce(ms.nome, tm.modulo_codigo) as ModuleName,
       tm.status as Status, tm.vigencia_inicio as EffectiveFrom, tm.vigencia_fim as EffectiveUntil,
       tm.plano_codigo as PlanCode, tm.valor_contratado as ContractedValue, tm.motivo_status as Reason,
       coalesce(tm.updated_at, tm.created_at) as UpdatedAt
from sigov.tenant_modulo_contratado tm
left join sigov.modulo_saas ms on ms.codigo=tm.modulo_codigo and ms.ativo and not ms.is_deleted
where tm.tenant_id=@TenantId
""";

    private const string CurrentSql = """
select id as Id, status as Status, created_at as CreatedAt, updated_at as UpdatedAt
from sigov.tenant_modulo_contratado
where tenant_id=@TenantId and modulo_codigo=@ModuleCode
""";

    private const string UpsertSql = """
insert into sigov.tenant_modulo_contratado (
    tenant_id, modulo_codigo, status, contratado_em, vigencia_inicio, vigencia_fim, motivo_status, ativo, created_by, correlation_id)
values (@TenantId, @ModuleCode, @Status, current_date, @VigenciaInicio, @EffectiveUntil, @Motivo, @Ativo, @UserId, @CorrelationId)
on conflict (tenant_id, modulo_codigo) do update
set status=excluded.status,
    vigencia_inicio=excluded.vigencia_inicio,
    vigencia_fim=excluded.vigencia_fim,
    motivo_status=excluded.motivo_status,
    ativo=excluded.ativo,
    updated_at=now(),
    updated_by=@UserId,
    correlation_id=@CorrelationId
""";

    private const string AuditSql = """
insert into sigov.auditoria_evento(tenant_id, usuario_id, acao, entidade, entidade_id, antes, depois, correlation_id)
values (@TenantId, @UserId, @Acao, 'sigov.tenant_modulo_contratado', @EntidadeId, @Antes::jsonb, @Depois::jsonb, @CorrelationId)
""";
}
