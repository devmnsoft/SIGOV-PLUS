using Sigov.Infrastructure.Outbox;

namespace Sigov.Worker.Outbox;

public sealed class OutboxJob : IOutboxJob
{
    private readonly IOutboxRepository _repository;
    private readonly IOutboxProcessor _processor;
    private readonly ILogger<OutboxJob> _logger;

    public OutboxJob(IOutboxRepository repository, IOutboxProcessor processor, ILogger<OutboxJob> logger)
    {
        _repository = repository;
        _processor = processor;
        _logger = logger;
    }

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        var correlationId = Guid.NewGuid();
        long? jobId = null;
        var processed = 0;
        try
        {
            jobId = await _repository.StartJobAsync(correlationId, cancellationToken).ConfigureAwait(false);
            var result = await _processor.ProcessBatchAsync(cancellationToken).ConfigureAwait(false);
            processed = result.Processed;
            await _repository.CompleteJobAsync(jobId.Value, result.Processed, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            if (jobId.HasValue)
            {
                await _repository.FailJobAsync(jobId.Value, processed, ex.Message, cancellationToken).ConfigureAwait(false);
            }

            _logger.LogError(ex, "Falha no ciclo do worker de outbox; o próximo ciclo continuará sem derrubar a aplicação. CorrelationId={CorrelationId}", correlationId);
        }
    }
}
