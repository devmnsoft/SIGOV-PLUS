using Sigov.Infrastructure.Outbox;

namespace Sigov.Worker.Outbox;

public sealed class OutboxProcessor : IOutboxProcessor
{
    private const int BatchSize = 20;
    private readonly IOutboxRepository _repository;
    private readonly IOutboxHandlerFactory _handlerFactory;
    private readonly IOutboxRetryPolicy _retryPolicy;
    private readonly ILogger<OutboxProcessor> _logger;

    public OutboxProcessor(IOutboxRepository repository, IOutboxHandlerFactory handlerFactory, IOutboxRetryPolicy retryPolicy, ILogger<OutboxProcessor> logger)
    {
        _repository = repository;
        _handlerFactory = handlerFactory;
        _retryPolicy = retryPolicy;
        _logger = logger;
    }

    public async Task<OutboxProcessingResult> ProcessBatchAsync(CancellationToken cancellationToken)
    {
        var messages = await _repository.FetchPendingAsync(BatchSize, cancellationToken).ConfigureAwait(false);
        var processed = 0;
        var failed = 0;

        foreach (var message in messages)
        {
            try
            {
                var workerMessage = new OutboxMessage(message.Id, message.TenantId, message.TipoEvento, message.Payload, message.Tentativas, message.MaxTentativas, message.CorrelationId);
                await _handlerFactory.Resolve(message.TipoEvento).HandleAsync(workerMessage, cancellationToken).ConfigureAwait(false);
                await _repository.MarkProcessedAsync(message, cancellationToken).ConfigureAwait(false);
                processed++;
                _logger.LogInformation("Outbox processada. EventId={EventId} TenantId={TenantId} TipoEvento={TipoEvento} CorrelationId={CorrelationId}", message.Id, message.TenantId, message.TipoEvento, message.CorrelationId);
            }
            catch (OutboxPermanentFailureException ex)
            {
                // RC-EVO-B §5: falha definitiva nomeada (config/payload/checksum/saldo) → dead-letter
                // imediato com o motivo rastreável; backoff seria queima de tentativas de algo que
                // só muda com intervenção do administrador (que pode reabrir a mensagem depois).
                failed++;
                await _repository.MarkFailureAsync(message, message.Tentativas + 1, true, TimeSpan.Zero, ex.Message, cancellationToken).ConfigureAwait(false);
                _logger.LogWarning("Evento outbox enviado para dead-letter (falha definitiva). EventId={EventId} TenantId={TenantId} TipoEvento={TipoEvento} CorrelationId={CorrelationId} Motivo={Motivo}", message.Id, message.TenantId, message.TipoEvento, message.CorrelationId, ex.Message);
            }
            catch (Exception ex)
            {
                failed++;
                var decision = _retryPolicy.Calculate(message.Tentativas, message.MaxTentativas);
                await _repository.MarkFailureAsync(message, decision.NextAttempts, decision.DeadLetter, decision.Delay, ex.Message, cancellationToken).ConfigureAwait(false);
                _logger.LogError(ex, "Falha em evento outbox. EventId={EventId} TenantId={TenantId} TipoEvento={TipoEvento} CorrelationId={CorrelationId}", message.Id, message.TenantId, message.TipoEvento, message.CorrelationId);
            }
        }

        return new OutboxProcessingResult(messages.Count, processed, failed);
    }
}
