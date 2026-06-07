namespace Sigov.Worker.Outbox;

public sealed class OutboxHandlerFactory : IOutboxHandlerFactory
{
    private readonly IReadOnlyCollection<IOutboxHandler> _handlers;

    public OutboxHandlerFactory(IEnumerable<IOutboxHandler> handlers) => _handlers = handlers.ToArray();

    public IOutboxHandler Resolve(string tipoEvento) => _handlers.First(handler => handler.CanHandle(tipoEvento));
}
