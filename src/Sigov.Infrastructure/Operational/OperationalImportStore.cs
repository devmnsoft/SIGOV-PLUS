using System.Text.Json;
using Dapper;
using Sigov.Application.Common;
using Sigov.Infrastructure.Persistence.Dapper;
using Sigov.Infrastructure.Persistence.Repositories;

namespace Sigov.Infrastructure.Operational;

public sealed class OperationalImportStore : BaseRepository, IOperationalImportStore
{
    private readonly DapperContext _context;
    public OperationalImportStore(DapperContext context) => _context = context;

    public async Task<long> SaveReportAsync(long tenantId, string module, string resource, int total, int persisted, int rejected, object detail, long? userId, string correlationId, CancellationToken ct)
    {
        const string sql = @"
            insert into sigov.relatorio_importacao
              (tenant_id, modulo, recurso, total_linhas, linhas_importadas, linhas_rejeitadas, status, detalhes, correlation_id, created_by, auditoria)
            values
              (@TenantId, @Module, @Resource, @Total, @Persisted, @Rejected, 'CONCLUIDA', cast(@Detail as jsonb), @CorrelationId, @UserId,
               jsonb_build_object('operacao','IMPORTAR','usuarioId',@UserId,'correlationId',@CorrelationId))
            returning id;
            ";
        using var connection = _context.CreateConnection();
        return await connection.ExecuteScalarAsync<long>(Command(sql, new { TenantId = tenantId, Module = module, Resource = resource, Total = total, Persisted = persisted, Rejected = rejected, Detail = JsonSerializer.Serialize(detail), UserId = userId, CorrelationId = correlationId }, ct)).ConfigureAwait(false);
    }

    public async Task<IReadOnlyCollection<OperationalAlertResponse>> ListAlertsAsync(long tenantId, string? module, string? severity, CancellationToken ct)
    {
        const string sql = @"
            select id, modulo as Module, tipo as Type, severidade as Severity, titulo as Title,
                   descricao as Description, status as Status, created_at as CreatedAt
              from sigov.alerta_operacional
             where tenant_id=@TenantId and is_deleted=false
               and (@Module is null or modulo=@Module) and (@Severity is null or severidade=@Severity)
             order by case severidade when 'CRITICA' then 1 when 'ALTA' then 2 else 3 end, created_at desc limit 250;
            ";
        using var connection = _context.CreateConnection();
        var rows = await connection.QueryAsync<OperationalAlertResponse>(Command(sql, new { TenantId = tenantId, Module = module, Severity = severity }, ct)).ConfigureAwait(false);
        return rows.AsList();
    }

    public async Task<bool> ResolveAlertAsync(long tenantId, long id, long? userId, string justification, string correlationId, CancellationToken ct)
    {
        const string sql = @"
            update sigov.alerta_operacional set status='RESOLVIDO', resolved_at=now(), resolved_by=@UserId, updated_at=now(),
                   dados=coalesce(dados,'{}'::jsonb)||jsonb_build_object('justificativaResolucao',@Justification),
                   auditoria=coalesce(auditoria,'{}'::jsonb)||jsonb_build_object('operacao','RESOLVER','usuarioId',@UserId,'correlationId',@CorrelationId)
             where tenant_id=@TenantId and id=@Id and is_deleted=false and status<>'RESOLVIDO';
            ";
        using var connection = _context.CreateConnection();
        return await connection.ExecuteAsync(Command(sql, new { TenantId = tenantId, Id = id, UserId = userId, Justification = justification, CorrelationId = correlationId }, ct)).ConfigureAwait(false) > 0;
    }
}
