namespace Sigov.Domain.Common;

public abstract class Entity
{
    private readonly List<DomainEvent> _domainEvents = new();

    public long Id { get; protected set; }
    public IReadOnlyCollection<DomainEvent> DomainEvents => _domainEvents.AsReadOnly();

    protected void AddDomainEvent(DomainEvent domainEvent) => _domainEvents.Add(domainEvent);

    public void ClearDomainEvents() => _domainEvents.Clear();
}
