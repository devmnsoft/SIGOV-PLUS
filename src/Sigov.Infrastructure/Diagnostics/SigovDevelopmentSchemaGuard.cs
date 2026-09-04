using Sigov.Infrastructure.Persistence.Dapper;

namespace Sigov.Infrastructure.Diagnostics;

public sealed class SigovDevelopmentSchemaGuard
{
    private readonly NpgsqlConnectionFactory _connectionFactory;

    public SigovDevelopmentSchemaGuard(NpgsqlConnectionFactory connectionFactory) =>
        _connectionFactory = connectionFactory;

    public async Task ValidateAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        await using var schemaCommand = connection.CreateCommand();
        schemaCommand.CommandText = "select to_regnamespace('sigov') is not null;";
        if (await schemaCommand.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false) is not true)
            throw new InvalidOperationException("Schema sigov não existe no database configurado. Crie o schema ou execute o script_completo_dev.sql.");

        await using var tableCommand = connection.CreateCommand();
        tableCommand.CommandText = "select to_regclass('sigov.usuario') is not null;";
        if (await tableCommand.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false) is not true)
            throw new InvalidOperationException("Tabela sigov.usuario não existe. Execute as migrations/script_completo_dev.sql.");
    }
}
