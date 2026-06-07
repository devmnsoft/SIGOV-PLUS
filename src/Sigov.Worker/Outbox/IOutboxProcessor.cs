namespace Sigov.Worker.Outbox;

public interface IOutboxProcessor
{
    Task<OutboxProcessingResult> ProcessBatchAsync(CancellationToken cancellationToken);
}
