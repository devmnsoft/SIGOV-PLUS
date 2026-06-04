namespace SIGOV.Domain.Common;

public abstract record DomainEvent(DateTimeOffset OccurredAt);
