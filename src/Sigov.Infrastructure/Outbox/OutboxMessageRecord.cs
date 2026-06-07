namespace Sigov.Infrastructure.Outbox;

public sealed record OutboxMessageRecord(
    long Id,
    long TenantId,
    string TipoEvento,
    string Payload,
    int Tentativas,
    int MaxTentativas,
    Guid? CorrelationId);
