using System.Data;
using Dapper;
using Sigov.Application.Saas.Comercial;
using Sigov.Infrastructure.Persistence.Dapper;

namespace Sigov.Infrastructure.Saas.Comercial;

public sealed class SaasLimitValidator : ISaasLimitValidator
{
    private readonly DapperContext _context;

    public SaasLimitValidator(DapperContext context) => _context = context;

    // Limite efetivo de usuários (RC-SAAS-AUT Etapa B): limite do plano + bônus dos addons
    // ATIVOS de tipo USUARIOS% (quantidade contratada x valor_quantidade do catálogo).
    // Plano ilimitado (limite_usuarios null) permanece ilimitado.
    private const string UsoBase = @"select @TenantId as TenantId, p.codigo as Plano,
case when p.limite_usuarios is null then null::int else p.limite_usuarios + coalesce((select sum(coalesce(aa.quantidade,1) * coalesce(add.valor_quantidade,1))::int
from sigov.saas_assinatura_addon aa join sigov.saas_addon add on add.codigo=aa.addon_codigo and add.ativo
where aa.assinatura_id=a.id and upper(aa.status)='ATIVO' and add.tipo_addon like 'USUARIOS%'),0) end as LimiteUsuarios,
coalesce((select count(*)::int from sigov.usuario u where u.tenant_id=@TenantId and u.ativo=true and u.is_deleted=false),0) as UsuariosAtivos,
coalesce((select count(*)::int from sigov.saas_assinatura_modulo m where m.tenant_id=@TenantId and m.habilitado=true),0) as ModulosAtivos,
null::int as LimiteModulos, p.permite_white_label as WhiteLabelPermitido, p.permite_dominio_customizado as DominioCustomizadoPermitido, a.data_fim as DataFim
from sigov.saas_assinatura a join sigov.saas_plano p on p.id=a.plano_id
where a.tenant_id=@TenantId and a.status='ATIVA' order by a.created_at desc limit 1";

    // RC-EVO S3.2: lock transacional single-table (mesma lição do GATE.G, RC-SAAS-AUT Etapa B): em
    // PG16/READ COMMITTED, SELECT de lock com JOIN + ORDER BY/LIMIT pode perder a linha no recheck
    // EvalPlanQual após aguardar o lock concorrente. A sentença trava todas as assinaturas ATIVAS do
    // tenant; a mais recente é escolhida em memória, preservando a semântica anterior (created_at desc).
    private const string LockAssinaturasAtivas = @"select id as Id, plano_id as PlanoId, created_at as CriadoEm, data_fim as DataFim
from sigov.saas_assinatura
where tenant_id=@TenantId and status='ATIVA'
for update";

    // Leitura do limite do plano executada APÓS adquirir o lock (nova sentença, snapshot fresco).
    private const string LimitePlanoApósLock = @"select p.codigo as Plano,
case when p.limite_usuarios is null then null::int else p.limite_usuarios + coalesce((select sum(coalesce(aa.quantidade,1) * coalesce(add.valor_quantidade,1))::int
from sigov.saas_assinatura_addon aa join sigov.saas_addon add on add.codigo=aa.addon_codigo and add.ativo
where aa.assinatura_id=@AssinaturaId and upper(aa.status)='ATIVO' and add.tipo_addon like 'USUARIOS%'),0) end as LimiteUsuarios,
p.permite_white_label as WhiteLabelPermitido, p.permite_dominio_customizado as DominioCustomizadoPermitido
from sigov.saas_plano p
where p.id=@PlanoId";

    // Contagens executadas como sentenças independentes enquanto o lock está mantido: cada uma toma
    // snapshot novo sob READ COMMITTED e vê os usuários commitados pela disputa serializada — é isso
    // que garante que, na última vaga do limite comercial, exatamente uma criação concorrente é admitida.
    private const string CountUsuariosAtivos = "select coalesce((select count(*)::int from sigov.usuario u where u.tenant_id=@TenantId and u.ativo=true and u.is_deleted=false),0)";
    private const string CountModulosAtivos = "select coalesce((select count(*)::int from sigov.saas_assinatura_modulo m where m.tenant_id=@TenantId and m.habilitado=true),0)";

    public async Task<SaasLimitValidationResult> ValidateUserLimitAsync(long tenantId, CancellationToken cancellationToken = default)
    {
        using var connection = _context.CreateConnection();
        var row = await connection.QuerySingleOrDefaultAsync<UsageRow>(new CommandDefinition(UsoBase, new { TenantId = tenantId }, cancellationToken: cancellationToken)).ConfigureAwait(false);
        return UserLimitDecision(tenantId, row);
    }

    /// <summary>
    /// Validação transacional do limite de usuários (RC-SAAS-AUT Etapa B / RC-EVO S3.2): primeiro trava as
    /// assinaturas ativas (<c>for update</c>, sentença single-table) para serializar criações concorrentes no
    /// mesmo tenant; depois conta os usuários ativos em sentença independente executada com o lock mantido.
    /// Sob PG/READ COMMITTED o snapshot fresco da contagem reflete o usuário commitado pela disputa
    /// serializada, então duas criações concorrentes pela última vaga admitem exatamente uma vencedora.
    /// </summary>
    public async Task<SaasLimitValidationResult> ValidateUserLimitTxAsync(IDbConnection connection, IDbTransaction transaction, long tenantId, CancellationToken cancellationToken = default)
    {
        var assinaturas = await connection.QueryAsync<AssinaturaAtivaLinha>(new CommandDefinition(LockAssinaturasAtivas, new { TenantId = tenantId }, transaction, cancellationToken: cancellationToken)).ConfigureAwait(false);
        var melhor = assinaturas.OrderByDescending(a => a.CriadoEm).FirstOrDefault();
        if (melhor is null) return UserLimitDecision(tenantId, null);

        var plano = await connection.QuerySingleOrDefaultAsync<PlanoLimiteLinha>(new CommandDefinition(LimitePlanoApósLock, new { AssinaturaId = melhor.Id, PlanoId = melhor.PlanoId }, transaction, cancellationToken: cancellationToken)).ConfigureAwait(false);
        if (plano is null) return UserLimitDecision(tenantId, null);

        var usuariosAtivos = await connection.ExecuteScalarAsync<int>(new CommandDefinition(CountUsuariosAtivos, new { TenantId = tenantId }, transaction, cancellationToken: cancellationToken)).ConfigureAwait(false);
        var modulosAtivos = await connection.ExecuteScalarAsync<int>(new CommandDefinition(CountModulosAtivos, new { TenantId = tenantId }, transaction, cancellationToken: cancellationToken)).ConfigureAwait(false);
        return UserLimitDecision(tenantId, new UsageRow(tenantId, plano.Plano, plano.LimiteUsuarios, usuariosAtivos, modulosAtivos, null, plano.WhiteLabelPermitido, plano.DominioCustomizadoPermitido, melhor.DataFim));
    }

    /// <summary>
    /// RC-EVO A: falha explícita de vigência (regra 13) — ausência de assinatura ativa nunca equivale a
    /// "ilimitado" e assinatura vencida (<c>data_fim &lt; hoje</c>) bloqueia a operação dependente.
    /// </summary>
    private static SaasLimitValidationResult UserLimitDecision(long tenantId, UsageRow? row)
    {
        var usage = BuildUsage(tenantId, row);
        if (row is null) return new SaasLimitValidationResult(false, "Nenhuma assinatura ativa para este tenant.", usage);
        if (row.DataFim is not null && row.DataFim < DateOnly.FromDateTime(DateTime.Today)) return new SaasLimitValidationResult(false, "Assinatura vencida.", usage);
        var allowed = usage.LimiteUsuarios is null || usage.UsuariosAtivos < usage.LimiteUsuarios;
        return new SaasLimitValidationResult(allowed, allowed ? null : "Limite de usuários do plano atingido.", usage);
    }

    private static SaasUsageSummary BuildUsage(long tenantId, UsageRow? row) => row is null
        ? new SaasUsageSummary(tenantId, null, 0, null, 0, null, false, false, 0)
        : new SaasUsageSummary(tenantId, row.Plano, row.UsuariosAtivos, row.LimiteUsuarios, row.ModulosAtivos, row.LimiteModulos, row.WhiteLabelPermitido, row.DominioCustomizadoPermitido, Percentual(row));

    public async Task<SaasLimitValidationResult> ValidateModuleLimitAsync(long tenantId, string moduloCodigo, CancellationToken cancellationToken = default)
    {
        using var connection = _context.CreateConnection();
        var allowed = await connection.ExecuteScalarAsync<bool>(new CommandDefinition(@"select exists(
select 1 from sigov.saas_assinatura a
join sigov.saas_plano_modulo pm on pm.plano_id=a.plano_id and pm.incluso=true
where a.tenant_id=@TenantId and a.status='ATIVA' and pm.modulo_codigo=@Modulo)", new { TenantId = tenantId, Modulo = moduloCodigo.Trim().ToLowerInvariant() }, cancellationToken: cancellationToken));
        var usage = await GetUsageSummaryAsync(tenantId, cancellationToken);
        return new SaasLimitValidationResult(allowed, allowed ? null : "Módulo não contratado no plano atual.", usage);
    }

    public async Task<SaasLimitValidationResult> ValidateWhiteLabelAllowedAsync(long tenantId, CancellationToken cancellationToken = default)
    {
        var usage = await GetUsageSummaryAsync(tenantId, cancellationToken);
        return new SaasLimitValidationResult(usage.WhiteLabelPermitido, usage.WhiteLabelPermitido ? null : "Plano não permite white label.", usage);
    }

    public async Task<SaasUsageSummary> GetUsageSummaryAsync(long tenantId, CancellationToken cancellationToken = default)
    {
        using var connection = _context.CreateConnection();
        var row = await connection.QuerySingleOrDefaultAsync<UsageRow>(new CommandDefinition(UsoBase, new { TenantId = tenantId }, cancellationToken: cancellationToken)).ConfigureAwait(false);
        // Resumo tolerante (uso em telas): não aplica o gate de vigência — ele pertence às validações.
        return BuildUsage(tenantId, row);
    }

    private static decimal Percentual(UsageRow row) =>
        row.LimiteUsuarios is null or 0 ? 0 : Math.Round((decimal)row.UsuariosAtivos * 100 / row.LimiteUsuarios.Value, 2);

    private sealed record UsageRow(long TenantId, string? Plano, int? LimiteUsuarios, int UsuariosAtivos, int ModulosAtivos, int? LimiteModulos, bool WhiteLabelPermitido, bool DominioCustomizadoPermitido, DateOnly? DataFim);

    // Linha da assinatura ativa obtida pelo lock single-table (RC-EVO S3.2).
    private sealed record AssinaturaAtivaLinha(long Id, long PlanoId, DateTime CriadoEm, DateOnly? DataFim);

    // Limite do plano lido após o lock (limite efetivo já soma os addons ATIVOS de USUARIOS%).
    private sealed record PlanoLimiteLinha(string? Plano, int? LimiteUsuarios, bool WhiteLabelPermitido, bool DominioCustomizadoPermitido);
}
