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
            users.Select(user => new SaasTenantUserItem(user.Id, user.Name, user.Email, user.Active, SplitProfiles(user.Profiles), user.Blocked)).ToArray(),
            contracts,
            catalogItems.Select(item => new ModuleCatalogOption(item.Codigo, item.Nome, item.Dependencias)).ToArray());
    }

    public Task<SaasModuleContractResult> ContractAsync(SaasModuleContractCommand command, long userId, string correlationId, CancellationToken cancellationToken = default) =>
        MutateAsync(command, userId, correlationId, "CONTRATADO", "SAAS_MODULO_CONTRATAR", cancellationToken);

    public Task<SaasModuleContractResult> SuspendAsync(SaasModuleContractCommand command, long userId, string correlationId, CancellationToken cancellationToken = default) =>
        MutateAsync(command, userId, correlationId, "SUSPENSO", "SAAS_MODULO_SUSPENDER", cancellationToken);

    public Task<SaasModuleContractResult> ReactivateAsync(SaasModuleContractCommand command, long userId, string correlationId, CancellationToken cancellationToken = default) =>
        MutateAsync(command, userId, correlationId, "HABILITADO", "SAAS_MODULO_REATIVAR", cancellationToken);

    public async Task<SaasModuleContractResult> ChangeTenantStatusAsync(SaasTenantStatusChangeCommand command, long userId, string correlationId, CancellationToken cancellationToken = default)
    {
        if (command.TenantId <= 0) return new(false, "Tenant obrigatório.");
        if (string.IsNullOrWhiteSpace(command.Justification) || command.Justification.Trim().Length < 5)
            return new(false, "Justificativa obrigatória com no mínimo 5 caracteres.");
        var target = command.TargetStatus?.ToUpperInvariant();
        if (target is not ("ATIVO" or "BLOQUEADO" or "SUSPENSO" or "INATIVO"))
            return new(false, "Status inválido para o tenant.");

        using var connection = context.CreateConnection();
        connection.Open();
        using var tx = connection.BeginTransaction();
        try
        {
            var current = await connection.QuerySingleOrDefaultAsync<string>("select upper(status) from sigov.tenant where id=@TenantId and not is_deleted for update", new { command.TenantId }, tx);
            if (current is null) return Rollback(tx, "Tenant não encontrado.");
            if (current == target) return new(true, $"Tenant já se encontra no status {target}.");

            var correlation = Guid.TryParse(correlationId, out var parsed) ? parsed : Guid.NewGuid();
            var ativo = target != "INATIVO";
            await connection.ExecuteAsync(new CommandDefinition(
                "update sigov.tenant set status=@Status, ativo=@Ativo, updated_at=now(), updated_by=@UserId, correlation_id=@CorrelationId where id=@TenantId",
                new { command.TenantId, Status = target, Ativo = ativo, UserId = userId, CorrelationId = correlation }, tx, cancellationToken: cancellationToken)).ConfigureAwait(false);

            await connection.ExecuteAsync(new CommandDefinition(
                AuditSql,
                new {
                    command.TenantId,
                    UserId = userId,
                    Acao = target == "BLOQUEADO" ? "SAAS_TENANT_BLOQUEAR" : target == "ATIVO" ? "SAAS_TENANT_DESBLOQUEAR" : "SAAS_TENANT_ALTERAR_STATUS",
                    EntidadeId = command.TenantId.ToString(),
                    CorrelationId = correlation,
                    Antes = JsonSerializer.Serialize(new { status = current }),
                    Depois = JsonSerializer.Serialize(new { status = target, justificativa = command.Justification })
                }, tx, cancellationToken: cancellationToken)).ConfigureAwait(false);

            tx.Commit();
            return new(true, $"Status do cliente alterado para {target}.");
        }
        catch (Exception)
        {
            tx.Rollback();
            throw;
        }
    }

    public async Task<SaasModuleContractResult> CreateUserAsync(SaasCreateUserCommand command, long userId, string correlationId, CancellationToken cancellationToken = default)
    {
        if (command.TenantId <= 0) return new(false, "Tenant obrigatório.");
        if (string.IsNullOrWhiteSpace(command.Name) || string.IsNullOrWhiteSpace(command.Email))
            return new(false, "Nome e e-mail são obrigatórios.");

        var login = string.IsNullOrWhiteSpace(command.Login) ? command.Email.Trim().ToLowerInvariant() : command.Login.Trim();
        using var connection = context.CreateConnection();
        connection.Open();
        using var tx = connection.BeginTransaction();
        try
        {
            var exists = await connection.ExecuteScalarAsync<bool>(
                "select exists(select 1 from sigov.usuario where (email=@Email or login=@Login) and tenant_id=@TenantId and not is_deleted)",
                new { command.TenantId, Email = command.Email.Trim(), Login = login }, tx);
            if (exists) return Rollback(tx, "Já existe usuário cadastrado com este e-mail ou login neste cliente.");

            var correlation = Guid.TryParse(correlationId, out var parsed) ? parsed : Guid.NewGuid();
            var hash = "$2a$11$e8p2i4/rA3vU9bkWc0H3z.qV9eFm7xU2vS0LzP5iU9j0eO2yP6yG2";
            var newUserId = await connection.ExecuteScalarAsync<long>(new CommandDefinition(
                """
                insert into sigov.usuario (tenant_id, nome, login, email, senha_hash, ativo, bloqueado, tipo_usuario, created_by, correlation_id)
                values (@TenantId, @Name, @Login, @Email, @Hash, true, false, @TipoUsuario, @UserId, @CorrelationId)
                returning id
                """,
                new { command.TenantId, Name = command.Name.Trim(), Login = login, Email = command.Email.Trim().ToLowerInvariant(), Hash = hash, TipoUsuario = command.TipoUsuario ?? "TENANT_USER", UserId = userId, CorrelationId = correlation }, tx, cancellationToken: cancellationToken)).ConfigureAwait(false);

            if (!string.IsNullOrWhiteSpace(command.PerfilCodigo))
            {
                var perfilId = await connection.ExecuteScalarAsync<long?>(
                    "select id from sigov.perfil_acesso where (codigo_externo=@Codigo or nome=@Codigo) and ativo and not is_deleted limit 1",
                    new { Codigo = command.PerfilCodigo }, tx);
                if (perfilId.HasValue)
                {
                    var grupoId = await connection.ExecuteScalarAsync<long?>(
                        "select gp.grupo_acesso_id from sigov.grupo_perfil gp where gp.perfil_acesso_id=@PerfilId and gp.ativo and not gp.is_deleted limit 1",
                        new { PerfilId = perfilId.Value }, tx);
                    if (grupoId.HasValue)
                    {
                        await connection.ExecuteAsync(
                            "insert into sigov.usuario_grupo(usuario_id, grupo_acesso_id, ativo, created_by, correlation_id) values (@UserId, @GrupoId, true, @CreatedBy, @CorrelationId) on conflict do nothing",
                            new { UserId = newUserId, GrupoId = grupoId.Value, CreatedBy = userId, CorrelationId = correlation }, tx);
                    }
                }
            }

            await connection.ExecuteAsync(new CommandDefinition(
                AuditSql,
                new {
                    command.TenantId,
                    UserId = userId,
                    Acao = "SAAS_USUARIO_CRIAR",
                    EntidadeId = newUserId.ToString(),
                    CorrelationId = correlation,
                    Antes = "{}",
                    Depois = JsonSerializer.Serialize(new { userId = newUserId, name = command.Name, email = command.Email, login })
                }, tx, cancellationToken: cancellationToken)).ConfigureAwait(false);

            tx.Commit();
            return new(true, $"Usuário {command.Name} criado com sucesso.");
        }
        catch (Exception)
        {
            tx.Rollback();
            throw;
        }
    }

    public async Task<SaasModuleContractResult> UpdateUserAsync(SaasUpdateUserCommand command, long userId, string correlationId, CancellationToken cancellationToken = default)
    {
        if (command.TenantId <= 0 || command.UserId <= 0) return new(false, "Tenant e usuário são obrigatórios.");
        if (string.IsNullOrWhiteSpace(command.Name) || string.IsNullOrWhiteSpace(command.Email))
            return new(false, "Nome e e-mail são obrigatórios.");

        using var connection = context.CreateConnection();
        connection.Open();
        using var tx = connection.BeginTransaction();
        try
        {
            var before = await connection.QuerySingleOrDefaultAsync<UserRow>(
                "select id as Id, coalesce(nome, login) as Name, email as Email, (ativo and not is_deleted) as Active, coalesce(bloqueado, false) as Blocked, null as Profiles from sigov.usuario where id=@UserId and tenant_id=@TenantId and not is_deleted for update",
                new { command.UserId, command.TenantId }, tx);
            if (before is null) return Rollback(tx, "Usuário não encontrado.");

            var correlation = Guid.TryParse(correlationId, out var parsed) ? parsed : Guid.NewGuid();
            await connection.ExecuteAsync(new CommandDefinition(
                "update sigov.usuario set nome=@Name, email=@Email, updated_at=now(), updated_by=@UserId, correlation_id=@CorrelationId where id=@TargetUserId and tenant_id=@TenantId",
                new { Name = command.Name.Trim(), Email = command.Email.Trim().ToLowerInvariant(), UserId = userId, CorrelationId = correlation, TargetUserId = command.UserId, command.TenantId }, tx, cancellationToken: cancellationToken)).ConfigureAwait(false);

            await connection.ExecuteAsync(new CommandDefinition(
                AuditSql,
                new {
                    command.TenantId,
                    UserId = userId,
                    Acao = "SAAS_USUARIO_EDITAR",
                    EntidadeId = command.UserId.ToString(),
                    CorrelationId = correlation,
                    Antes = JsonSerializer.Serialize(before),
                    Depois = JsonSerializer.Serialize(new { name = command.Name, email = command.Email })
                }, tx, cancellationToken: cancellationToken)).ConfigureAwait(false);

            tx.Commit();
            return new(true, "Dados do usuário atualizados com sucesso.");
        }
        catch (Exception)
        {
            tx.Rollback();
            throw;
        }
    }

    public async Task<SaasModuleContractResult> ChangeUserStatusAsync(SaasUserStatusChangeCommand command, long userId, string correlationId, CancellationToken cancellationToken = default)
    {
        if (command.TenantId <= 0 || command.UserId <= 0) return new(false, "Tenant e usuário são obrigatórios.");
        if (string.IsNullOrWhiteSpace(command.Justification) || command.Justification.Trim().Length < 5)
            return new(false, "Justificativa obrigatória com no mínimo 5 caracteres.");

        using var connection = context.CreateConnection();
        connection.Open();
        using var tx = connection.BeginTransaction();
        try
        {
            var before = await connection.QuerySingleOrDefaultAsync<UserRow>(
                "select id as Id, coalesce(nome, login) as Name, email as Email, (ativo and not is_deleted) as Active, coalesce(bloqueado, false) as Blocked, null as Profiles from sigov.usuario where id=@UserId and tenant_id=@TenantId and not is_deleted for update",
                new { command.UserId, command.TenantId }, tx);
            if (before is null) return Rollback(tx, "Usuário não encontrado.");

            var correlation = Guid.TryParse(correlationId, out var parsed) ? parsed : Guid.NewGuid();
            await connection.ExecuteAsync(new CommandDefinition(
                "update sigov.usuario set ativo=@Active, bloqueado=@Blocked, updated_at=now(), updated_by=@UserId, correlation_id=@CorrelationId where id=@TargetUserId and tenant_id=@TenantId",
                new { Active = command.Active, Blocked = command.Blocked, UserId = userId, CorrelationId = correlation, TargetUserId = command.UserId, command.TenantId }, tx, cancellationToken: cancellationToken)).ConfigureAwait(false);

            var acao = command.Blocked ? "SAAS_USUARIO_BLOQUEAR" : (!command.Active ? "SAAS_USUARIO_INATIVAR" : "SAAS_USUARIO_ATIVAR");
            await connection.ExecuteAsync(new CommandDefinition(
                AuditSql,
                new {
                    command.TenantId,
                    UserId = userId,
                    Acao = acao,
                    EntidadeId = command.UserId.ToString(),
                    CorrelationId = correlation,
                    Antes = JsonSerializer.Serialize(before),
                    Depois = JsonSerializer.Serialize(new { active = command.Active, blocked = command.Blocked, justificativa = command.Justification })
                }, tx, cancellationToken: cancellationToken)).ConfigureAwait(false);

            tx.Commit();
            var desc = command.Blocked ? "bloqueado" : (!command.Active ? "inativado" : "desbloqueado/ativado");
            return new(true, $"Usuário {desc} com sucesso.");
        }
        catch (Exception)
        {
            tx.Rollback();
            throw;
        }
    }

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

        var requestedStart = command.EffectiveFrom ?? DateOnly.FromDateTime(DateTime.UtcNow);
        if (command.EffectiveUntil.HasValue && command.EffectiveUntil < requestedStart)
            return new(false, "A data final da vigência não pode ser anterior à data inicial.");
        if (!string.Equals(targetStatus, "CONTRATADO", StringComparison.OrdinalIgnoreCase) && !command.ExpectedUpdatedAt.HasValue)
            return new(false, "A versão atual do contrato é obrigatória. Recarregue o detalhe antes de executar a ação.");

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
            var isNewContract = string.Equals(targetStatus, "CONTRATADO", StringComparison.OrdinalIgnoreCase);
            var isSuspension = string.Equals(targetStatus, "SUSPENSO", StringComparison.OrdinalIgnoreCase);
            var isReactivation = string.Equals(targetStatus, "HABILITADO", StringComparison.OrdinalIgnoreCase);

            if (isNewContract && before is not null)
                return Rollback(transaction, "Já existe contratação ou histórico contratual para este módulo; recarregue o detalhe e use a ação compatível com o estado atual.");
            if (isSuspension || isReactivation)
            {
                if (before is null)
                    return Rollback(transaction, "A contratação não existe ou foi alterada. Recarregue o detalhe.");

                var currentContract = before;
                if (isSuspension && currentContract.Status is not ("CONTRATADO" or "HABILITADO" or "ATIVO" or "TRIAL" or "EM_IMPLANTACAO" or "BETA"))
                    return Rollback(transaction, $"Uma contratação no estado {currentContract.Status} não pode ser suspensa.");
                if (isReactivation && !string.Equals(currentContract.Status, "SUSPENSO", StringComparison.OrdinalIgnoreCase))
                    return Rollback(transaction, $"Somente uma contratação suspensa pode ser reativada; estado atual: {currentContract.Status}.");

                var todayUtc = DateOnly.FromDateTime(DateTime.UtcNow);
                if (isReactivation && currentContract.EffectiveUntil is { } effectiveUntil && effectiveUntil < todayUtc)
                    return Rollback(transaction, "A vigência terminou; reativação não renova o contrato automaticamente.");
                if (isReactivation && currentContract.CancellationScheduledFor is { } cancellationDate && cancellationDate <= todayUtc)
                    return Rollback(transaction, "O cancelamento agendado já produziu efeito; reativação exige nova decisão contratual.");
            }
            if (command.ExpectedUpdatedAt.HasValue && before is not null &&
                before.UpdatedAt != command.ExpectedUpdatedAt && before.CreatedAt != command.ExpectedUpdatedAt)
                return Rollback(transaction, "O contrato foi alterado por outro usuário. Recarregue e tente novamente.");

            var correlation = Guid.TryParse(correlationId, out var parsed) ? parsed : Guid.NewGuid();
            var from = requestedStart;
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
                    Ativo = !string.Equals(targetStatus, "CANCELADO", StringComparison.OrdinalIgnoreCase),
                    PreserveTerm = !isNewContract
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
    private sealed record UserRow(long Id, string Name, string Email, bool Active, string? Profiles, bool Blocked = false);
    private sealed record ContractRow(long Id, string Status, DateOnly? EffectiveFrom, DateOnly? EffectiveUntil,
        DateOnly? CancellationScheduledFor, DateTimeOffset CreatedAt, DateTimeOffset? UpdatedAt);

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
       coalesce(u.bloqueado, false) as Blocked,
       string_agg(distinct coalesce(pa.codigo_externo, pa.nome), ',') as Profiles
from sigov.usuario u
left join sigov.usuario_grupo ug on ug.usuario_id=u.id and ug.ativo and not ug.is_deleted
left join sigov.grupo_perfil gp on gp.grupo_acesso_id=ug.grupo_acesso_id and gp.ativo and not gp.is_deleted
left join sigov.perfil_acesso pa on pa.id=gp.perfil_acesso_id and pa.ativo and not pa.is_deleted
where u.tenant_id=@TenantId
group by u.id, u.nome, u.email, u.ativo, u.is_deleted, u.bloqueado
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
select id as Id, status as Status, vigencia_inicio as EffectiveFrom, vigencia_fim as EffectiveUntil,
       cancelamento_agendado_para as CancellationScheduledFor, created_at as CreatedAt, updated_at as UpdatedAt
from sigov.tenant_modulo_contratado
where tenant_id=@TenantId and modulo_codigo=@ModuleCode
""";

    private const string UpsertSql = """
insert into sigov.tenant_modulo_contratado (
    tenant_id, modulo_codigo, status, contratado_em, vigencia_inicio, vigencia_fim, motivo_status, ativo, created_by, correlation_id)
values (@TenantId, @ModuleCode, @Status, current_date, @VigenciaInicio, @EffectiveUntil, @Motivo, @Ativo, @UserId, @CorrelationId)
on conflict (tenant_id, modulo_codigo) do update
set status=excluded.status,
    vigencia_inicio=case when @PreserveTerm then tenant_modulo_contratado.vigencia_inicio else excluded.vigencia_inicio end,
    vigencia_fim=case when @PreserveTerm then tenant_modulo_contratado.vigencia_fim else excluded.vigencia_fim end,
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
