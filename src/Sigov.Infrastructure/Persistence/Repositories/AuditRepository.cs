using Dapper;
using Microsoft.Extensions.Logging;
using Sigov.Application.Abstractions;
using Sigov.Infrastructure.Persistence.Dapper;

namespace Sigov.Infrastructure.Persistence.Repositories;

public sealed record TrilhaAuditoriaDto(long Id, string Tabela, string Acao, DateTimeOffset CreatedAt);

public sealed class AuditRepository : BaseRepository
{
    private readonly DapperContext _context;
    private readonly ILogger<AuditRepository> _logger;
    private readonly ICurrentTenant _currentTenant;

    public AuditRepository(DapperContext context, ILogger<AuditRepository> logger, ICurrentTenant currentTenant)
    {
        _context = context;
        _logger = logger;
        _currentTenant = currentTenant;
    }

    public async Task<IReadOnlyCollection<TrilhaAuditoriaDto>> ListarAsync(int page, int pageSize, CancellationToken cancellationToken)
    {
        if (!_currentTenant.TenantId.HasValue)
        {
            throw new InvalidOperationException("TenantId obrigatório para listar auditoria operacional.");
        }

        try
        {
            var safePage = page < 1 ? 1 : page;
            var safePageSize = pageSize is < 1 or > 100 ? 20 : pageSize;
            const string sql = """
                select id, tabela, acao, created_at as CreatedAt
                from sigov.trilha_auditoria
                where tenant_id = @TenantId
                  and is_deleted = false
                order by created_at desc
                limit @PageSize offset @Offset;
                """;

            using var connection = _context.CreateConnection();
            var rows = await connection.QueryAsync<TrilhaAuditoriaDto>(Command(sql, new { TenantId = _currentTenant.TenantId.Value, PageSize = safePageSize, Offset = (safePage - 1) * safePageSize }, cancellationToken)).ConfigureAwait(false);
            return rows.AsList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao listar trilhas de auditoria no schema sigov. TenantId={TenantId}", _currentTenant.TenantId);
            throw;
        }
    }
}
