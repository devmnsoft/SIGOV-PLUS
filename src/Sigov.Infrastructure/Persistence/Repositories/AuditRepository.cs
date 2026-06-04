using Dapper;
using Microsoft.Extensions.Logging;
using Sigov.Infrastructure.Persistence.Dapper;

namespace Sigov.Infrastructure.Persistence.Repositories;

public sealed record TrilhaAuditoriaDto(long Id, string Tabela, string Acao, DateTimeOffset CreatedAt);

public sealed class AuditRepository : BaseRepository
{
    private readonly DapperContext _context;
    private readonly ILogger<AuditRepository> _logger;

    public AuditRepository(DapperContext context, ILogger<AuditRepository> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<IReadOnlyCollection<TrilhaAuditoriaDto>> ListarAsync(int page, int pageSize, CancellationToken cancellationToken)
    {
        try
        {
            var safePage = page < 1 ? 1 : page;
            var safePageSize = pageSize is < 1 or > 100 ? 20 : pageSize;
            const string sql = """
                select id, tabela, acao, created_at as CreatedAt
                from sigov.trilha_auditoria
                where is_deleted = false
                order by created_at desc
                limit @PageSize offset @Offset;
                """;

            using var connection = _context.CreateConnection();
            var rows = await connection.QueryAsync<TrilhaAuditoriaDto>(Command(sql, new { PageSize = safePageSize, Offset = (safePage - 1) * safePageSize }, cancellationToken)).ConfigureAwait(false);
            return rows.AsList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao listar trilhas de auditoria no schema sigov.");
            throw;
        }
    }
}
