using Sigov.Worker.Outbox;

namespace Sigov.Worker.Outbox.Handlers;

public sealed class WebhookOutboxHandler : DefaultOutboxHandler
{
    public override bool CanHandle(string tipoEvento) => string.Equals(tipoEvento, "WebhookEnviado", StringComparison.OrdinalIgnoreCase);
}
