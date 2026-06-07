using Sigov.Worker.Outbox;

namespace Sigov.Worker.Outbox.Handlers;

public sealed class IntegracaoOutboxHandler : DefaultOutboxHandler
{
    public override bool CanHandle(string tipoEvento) => tipoEvento.Contains("Integracao", StringComparison.OrdinalIgnoreCase) || string.Equals(tipoEvento, "SaasTenantProvisionado", StringComparison.OrdinalIgnoreCase);
}
