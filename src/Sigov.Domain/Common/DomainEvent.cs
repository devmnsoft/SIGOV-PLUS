namespace Sigov.Domain.Common;

public abstract record DomainEvent(DateTimeOffset OccurredAt);
