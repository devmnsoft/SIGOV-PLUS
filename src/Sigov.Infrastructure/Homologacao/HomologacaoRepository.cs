using Dapper;
using Sigov.Infrastructure.Persistence.Dapper;

namespace Sigov.Infrastructure.Homologacao;

public sealed class HomologacaoRepository
{
    private readonly NpgsqlConnectionFactory _connectionFactory;

    public HomologacaoRepository(NpgsqlConnectionFactory connectionFactory) => _connectionFactory = connectionFactory;

    public async Task<bool> TenantExistsAsync(string tenantSlug, CancellationToken cancellationToken)
    {
        await using var connection = _connectionFactory.CreateConnection();
        var result = await connection.ExecuteScalarAsync<int?>(new CommandDefinition(
            HomologacaoSql.EnsureTenant,
            new { TenantSlug = tenantSlug },
            cancellationToken: cancellationToken)).ConfigureAwait(false);
        return result.HasValue;
    }
}
