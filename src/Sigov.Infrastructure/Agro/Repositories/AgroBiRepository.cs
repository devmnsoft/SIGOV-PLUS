using Dapper;
using Sigov.Application.Agro.Bi;
using Sigov.Infrastructure.Agro.Sql;
using Sigov.Infrastructure.Persistence.Dapper;

namespace Sigov.Infrastructure.Agro.Repositories;

public sealed class AgroBiRepository : IAgroBiRepository
{
    private readonly DapperContext _context;
    public AgroBiRepository(DapperContext context) => _context = context;
    public async Task<AgroBiDashboardResponse> ObterDashboardAsync(long tenantId, long? entidadeId, long? exercicioId, CancellationToken cancellationToken)
    {
        using var cn = _context.CreateConnection();
        var row = await cn.QuerySingleOrDefaultAsync(new CommandDefinition(AgroBiSql.Dashboard, new { TenantId = tenantId, EntidadeId = entidadeId, ExercicioId = exercicioId }, cancellationToken: cancellationToken)).ConfigureAwait(false);
        var dict = row is null ? new Dictionary<string, decimal>() : ((IDictionary<string, object>)row).Where(kv => kv.Key is not ("TenantId" or "EntidadeId")).ToDictionary(kv => kv.Key, kv => Convert.ToDecimal(kv.Value ?? 0));
        var cards = dict.Select(kv => new AgroBiIndicadorCard(ToSnake(kv.Key), Humanize(kv.Key), Categoria(kv.Key), kv.Value, Unidade(kv.Key), true)).ToArray();
        return new AgroBiDashboardResponse(tenantId, entidadeId, cards, dict);
    }
    private static string ToSnake(string value) => string.Concat(value.Select((c, i) => i > 0 && char.IsUpper(c) ? "_" + char.ToLowerInvariant(c) : char.ToLowerInvariant(c).ToString()));
    private static string Humanize(string value) => string.Concat(value.Select((c, i) => i > 0 && char.IsUpper(c) ? " " + c : c.ToString()));
    private static string Categoria(string key) => key.Contains("Estrada", StringComparison.OrdinalIgnoreCase) ? "ESTRADAS" : key.Contains("Feira", StringComparison.OrdinalIgnoreCase) ? "FEIRAS" : key.Contains("Agroindustria", StringComparison.OrdinalIgnoreCase) ? "AGROINDUSTRIA" : key.Contains("Produc", StringComparison.OrdinalIgnoreCase) ? "PRODUCAO" : "PRODUTORES";
    private static string? Unidade(string key) => key.Contains("Area", StringComparison.OrdinalIgnoreCase) ? "ha" : key.Contains("Km", StringComparison.OrdinalIgnoreCase) ? "km" : null;
}
