namespace Sigov.Worker.Outbox;

public sealed record OutboxMessage(
    long Id,
    long TenantId,
    string TipoEvento,
    string Payload,
    int Tentativas,
    int MaxTentativas,
    Guid? CorrelationId);
