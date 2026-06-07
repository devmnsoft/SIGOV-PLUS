using Sigov.Worker.Outbox;

namespace Sigov.Worker.Outbox.Handlers;

public class DefaultOutboxHandler : IOutboxHandler
{
    public virtual bool CanHandle(string tipoEvento) => true;

    public virtual Task HandleAsync(OutboxMessage message, CancellationToken cancellationToken) => Task.CompletedTask.WaitAsync(cancellationToken);
}
