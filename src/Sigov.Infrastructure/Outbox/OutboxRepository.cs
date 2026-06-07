using Dapper;
using Sigov.Infrastructure.Persistence.Dapper;

namespace Sigov.Infrastructure.Outbox;

public sealed class OutboxRepository : IOutboxRepository
{
    private readonly DapperContext _context;

    public OutboxRepository(DapperContext context) => _context = context;

    public async Task<long> StartJobAsync(Guid correlationId, CancellationToken cancellationToken)
    {
        using var connection = _context.CreateConnection();
        return await connection.ExecuteScalarAsync<long>(new CommandDefinition(OutboxSqlQueries.StartJob, new { CorrelationId = correlationId }, cancellationToken: cancellationToken)).ConfigureAwait(false);
    }

    public async Task<IReadOnlyCollection<OutboxMessageRecord>> FetchPendingAsync(int batchSize, CancellationToken cancellationToken)
    {
        using var connection = _context.CreateConnection();
        var rows = await connection.QueryAsync<OutboxMessageRecord>(new CommandDefinition(OutboxSqlQueries.FetchPending, new { BatchSize = batchSize }, cancellationToken: cancellationToken)).ConfigureAwait(false);
        return rows.AsList();
    }

    public async Task MarkProcessedAsync(OutboxMessageRecord message, CancellationToken cancellationToken)
    {
        using var connection = _context.CreateConnection();
        await connection.ExecuteAsync(new CommandDefinition(OutboxSqlQueries.MarkProcessed, new { message.Id, message.TenantId, message.TipoEvento, message.CorrelationId }, cancellationToken: cancellationToken)).ConfigureAwait(false);
    }

    public async Task MarkFailureAsync(OutboxMessageRecord message, int nextAttempts, bool deadLetter, TimeSpan delay, string error, CancellationToken cancellationToken)
    {
        using var connection = _context.CreateConnection();
        await connection.ExecuteAsync(new CommandDefinition(OutboxSqlQueries.MarkFailure, new { message.Id, message.TenantId, message.TipoEvento, Tentativas = nextAttempts, DeadLetter = deadLetter, Erro = error, DelaySeconds = (int)delay.TotalSeconds, message.CorrelationId }, cancellationToken: cancellationToken)).ConfigureAwait(false);
    }

    public async Task CompleteJobAsync(long jobId, int processed, CancellationToken cancellationToken)
    {
        using var connection = _context.CreateConnection();
        await connection.ExecuteAsync(new CommandDefinition(OutboxSqlQueries.CompleteJob, new { JobId = jobId, Processed = processed }, cancellationToken: cancellationToken)).ConfigureAwait(false);
    }

    public async Task FailJobAsync(long jobId, int processed, string error, CancellationToken cancellationToken)
    {
        using var connection = _context.CreateConnection();
        await connection.ExecuteAsync(new CommandDefinition(OutboxSqlQueries.FailJob, new { JobId = jobId, Processed = processed, Erro = error }, cancellationToken: cancellationToken)).ConfigureAwait(false);
    }
}
