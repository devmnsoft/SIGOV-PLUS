namespace Sigov.Infrastructure.Outbox;

public interface IOutboxRepository
{
    Task<long> StartJobAsync(Guid correlationId, CancellationToken cancellationToken);
    Task<IReadOnlyCollection<OutboxMessageRecord>> FetchPendingAsync(int batchSize, CancellationToken cancellationToken);
    Task MarkProcessedAsync(OutboxMessageRecord message, CancellationToken cancellationToken);
    Task MarkFailureAsync(OutboxMessageRecord message, int nextAttempts, bool deadLetter, TimeSpan delay, string error, CancellationToken cancellationToken);
    Task CompleteJobAsync(long jobId, int processed, CancellationToken cancellationToken);
    Task FailJobAsync(long jobId, int processed, string error, CancellationToken cancellationToken);
}
