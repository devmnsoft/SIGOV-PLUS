using Sigov.Worker.Outbox;

namespace Sigov.Worker.Outbox.Handlers;

public sealed class SuporteOutboxHandler : DefaultOutboxHandler
{
    public override bool CanHandle(string tipoEvento) => tipoEvento.Contains("Suporte", StringComparison.OrdinalIgnoreCase);
}
