using Dapper;
using Microsoft.Extensions.Logging;
using Sigov.Application.Saas.SuperAdmin;
using Sigov.Infrastructure.Persistence.Dapper;

namespace Sigov.Infrastructure.Saas;

public sealed class SuperAdminOperationalDashboardService(DapperContext context, ILogger<SuperAdminOperationalDashboardService> logger)
    : ISuperAdminOperationalDashboardService
{
    public async Task<SuperAdminOperationalDashboard> GetAsync(SuperAdminDashboardFilter filter, CancellationToken cancellationToken = default)
    {
        var checkedAt = DateTimeOffset.UtcNow;
        try
        {
            using var connection = context.CreateConnection();
            await connection.ExecuteScalarAsync<int>(new CommandDefinition("select 1", cancellationToken: cancellationToken));
            var tables = (await connection.QueryAsync<string>(new CommandDefinition(
                "select table_name from information_schema.tables where table_schema='sigov'", cancellationToken: cancellationToken))).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var columns = (await connection.QueryAsync<string>(new CommandDefinition(
                "select table_name||'.'||column_name from information_schema.columns where table_schema='sigov'", cancellationToken: cancellationToken))).ToHashSet(StringComparer.OrdinalIgnoreCase);
            if (!tables.Contains("tenant")) return Unavailable(checkedAt, "Schema principal não configurado.");

            var args = new { filter.TenantId, filter.FromUtc, filter.ToUtc, Module = NullIfBlank(filter.Module), Status = NullIfBlank(filter.Status) };
            var tenants = (await connection.QueryAsync<SuperAdminTenantSummary>(new CommandDefinition(TenantSql, args, cancellationToken: cancellationToken))).AsList();
            var authAvailable = Has(columns, "autorizacao_decisao_auditoria", "decidido_em_utc", "tenant_id", "recurso", "acao", "permitido", "efeito", "motivo", "modulo");
            var contextAvailable = Has(columns, "contexto_operacional_auditoria", "ocorrido_at", "tenant_novo_id", "tenant_anterior_id", "resultado", "codigo_motivo");
            var auditAvailable = Has(columns, "auditoria_evento", "created_at", "tenant_id", "acao");
            var auth = authAvailable
                ? (await connection.QueryAsync<SuperAdminAuthorizationSummary>(new CommandDefinition(AuthorizationSql, args, cancellationToken: cancellationToken))).AsList() : [];
            var contexts = contextAvailable
                ? (await connection.QueryAsync<SuperAdminContextSummary>(new CommandDefinition(ContextSql, args, cancellationToken: cancellationToken))).AsList() : [];
            var audits = auditAvailable
                ? (await connection.QueryAsync<SuperAdminAuditSummary>(new CommandDefinition(AuditSql, args, cancellationToken: cancellationToken))).AsList() : [];

            var operations = new List<SuperAdminOperationSummary>();
            if (Has(columns, "outbox_evento", "status", "processed_at", "created_at", "tenant_id"))
                operations.AddRange(await connection.QueryAsync<SuperAdminOperationSummary>(new CommandDefinition(OutboxSql, args, cancellationToken: cancellationToken)));
            AddOptionalNotice(operations, tables, "webhook", "Webhooks");
            AddOptionalNotice(operations, tables, "worker_heartbeat", "Workers/jobs");
            AddOptionalNotice(operations, tables, "lgpd_requisicao", "Requisições LGPD");

            var kpis = new List<SuperAdminDashboardKpi>
            {
                new("tenants.total", "Tenants", tenants.Count, "Disponível"),
                new("tenants.active", "Tenants ativos", tenants.Count(x => x.Status.Equals("ATIVO", StringComparison.OrdinalIgnoreCase)), "Disponível"),
                new("authorization.allowed", "Autorizações permitidas", auth.Count(x => x.Allowed), authAvailable ? "Disponível" : "Indisponível"),
                new("authorization.denied", "Autorizações negadas", auth.Count(x => !x.Allowed), authAvailable ? "Disponível" : "Indisponível")
            };
            var alerts = tenants.Where(x => !x.ContextComplete).Select(x => new SuperAdminDashboardAlert("Contexto", "warning", $"Tenant {x.Id} sem contexto operacional completo.", x.Id)).ToList();
            if (!authAvailable) alerts.Add(new("Schema", "warning", "Área de autorização indisponível: schema incompatível."));
            if (!contextAvailable) alerts.Add(new("Schema", "warning", "Área de contexto indisponível: schema incompatível."));
            return new(checkedAt, "Disponível", "Disponível", kpis, alerts, tenants, auth, audits, contexts, operations);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Dashboard operacional indisponível; nenhum dado substituto será retornado.");
            return Unavailable(checkedAt, "Banco ou configuração indisponível.");
        }
    }

    private static string? NullIfBlank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private static bool Has(HashSet<string> columns, string table, params string[] required) => required.All(column => columns.Contains($"{table}.{column}"));
    private static void AddOptionalNotice(List<SuperAdminOperationSummary> items, HashSet<string> tables, string table, string area)
    { if (!tables.Contains(table)) items.Add(new(area, "Não configurado", 0, null)); }
    private static SuperAdminOperationalDashboard Unavailable(DateTimeOffset at, string message) => new(at, "Indisponível", "Indisponível", [], [new("Plataforma", "danger", message)], [], [], [], [], []);

    private const string TenantSql = """
select t.id as Id, coalesce(t.nome_fantasia,t.nome,t.slug,'Tenant '||t.id::text) as Name,
 upper(coalesce(t.status,'INATIVO')) as Status, p.nome as Plan,
 count(distinct tm.id)::int as Modules, count(distinct td.id)::int as Domains,
 count(distinct te.entidade_id)::int as Entities, count(distinct e.id)::int as FiscalYears,
 count(distinct u.id)::int as Units,
 (count(distinct te.entidade_id)>0 and count(distinct e.id)>0 and count(distinct u.id)>0) as ContextComplete
from sigov.tenant t
left join sigov.tenant_assinatura a on a.tenant_id=t.id and a.ativo and not a.is_deleted
left join sigov.plano_saas p on p.id=a.plano_saas_id
left join sigov.tenant_modulo tm on tm.tenant_id=t.id and tm.ativo and tm.contratado
left join sigov.tenant_dominio td on td.tenant_id=t.id and td.ativo
left join sigov.tenant_entidade te on te.tenant_id=t.id and te.ativo
left join sigov.exercicio e on e.entidade_id=te.entidade_id and e.ativo and not e.is_deleted
left join sigov.unidade_organizacional u on u.entidade_id=te.entidade_id and u.ativo and not u.is_deleted
where (@TenantId is null or t.id=@TenantId) and (@Status is null or upper(t.status)=upper(@Status))
group by t.id,t.nome_fantasia,t.nome,t.slug,t.status,p.nome order by t.id limit 250
""";
    private const string AuthorizationSql = """
select decidido_em_utc as AtUtc,tenant_id as TenantId,recurso as Resource,acao as Action,permitido as Allowed,
 (efeito='NEGAR') as ExplicitDeny,motivo as Reason from sigov.autorizacao_decisao_auditoria
where decidido_em_utc between @FromUtc and @ToUtc and (@TenantId is null or tenant_id=@TenantId)
and (@Module is null or modulo=@Module) order by decidido_em_utc desc limit 100
""";
    private const string ContextSql = """
select ocorrido_at as AtUtc,coalesce(tenant_novo_id,tenant_anterior_id) as TenantId,resultado as Result,codigo_motivo as Reason
from sigov.contexto_operacional_auditoria where ocorrido_at between @FromUtc and @ToUtc
and (@TenantId is null or tenant_novo_id=@TenantId or tenant_anterior_id=@TenantId) order by ocorrido_at desc limit 100
""";
    private const string AuditSql = """
select created_at as AtUtc,tenant_id as TenantId,'Auditoria' as Area,acao as Event,'REGISTRADO' as Result,true as Sensitive
from sigov.auditoria_evento where created_at between @FromUtc and @ToUtc and (@TenantId is null or tenant_id=@TenantId) order by created_at desc limit 100
""";
    private const string OutboxSql = """
select 'Outbox' as Area,coalesce(status,'PENDENTE') as Status,count(*)::bigint as Count,max(coalesce(processed_at,created_at)) as LastAtUtc
from sigov.outbox_evento where created_at between @FromUtc and @ToUtc and (@TenantId is null or tenant_id=@TenantId) group by status
""";
}
