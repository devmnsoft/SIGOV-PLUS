using Dapper;
using Sigov.Infrastructure.Persistence.Dapper;

namespace Sigov.Infrastructure.Onboarding;

public sealed record OnboardingTaskRecord(long TenantId, long JourneyId, string Code, string Title, string Description, int Order, bool Required, string Status, string Route, string MetadataJson);

public sealed class OnboardingRepository
{
    private readonly DapperContext _context;

    public OnboardingRepository(DapperContext context) => _context = context;

    public async Task<IReadOnlyList<OnboardingTaskRecord>> ListTasksAsync(long tenantId, long journeyId, CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT tenant_id AS TenantId,
                   jornada_id AS JourneyId,
                   codigo AS Code,
                   titulo AS Title,
                   descricao AS Description,
                   ordem AS Order,
                   obrigatoria AS Required,
                   status AS Status,
                   COALESCE(rota_destino, '') AS Route,
                   metadados::text AS MetadataJson
              FROM sigov.onboarding_tarefa
             WHERE tenant_id = @TenantId
               AND jornada_id = @JourneyId
             ORDER BY ordem;
            """;

        cancellationToken.ThrowIfCancellationRequested();
        using var connection = _context.CreateConnection();
        var rows = await connection.QueryAsync<OnboardingTaskRecord>(sql, new { TenantId = tenantId, JourneyId = journeyId }).ConfigureAwait(false);
        return rows.ToArray();
    }
}
