using Sigov.Worker.Outbox;

namespace Sigov.Worker.Outbox.Handlers;

public sealed class FinanceiroOutboxHandler : DefaultOutboxHandler
{
    public override bool CanHandle(string tipoEvento) => tipoEvento.Contains("Financeira", StringComparison.OrdinalIgnoreCase) || tipoEvento.Contains("Tributario", StringComparison.OrdinalIgnoreCase);
}
