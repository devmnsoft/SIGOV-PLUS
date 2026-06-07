namespace Sigov.Worker.Outbox;

public interface IOutboxHandler
{
    bool CanHandle(string tipoEvento);
    Task HandleAsync(OutboxMessage message, CancellationToken cancellationToken);
}
