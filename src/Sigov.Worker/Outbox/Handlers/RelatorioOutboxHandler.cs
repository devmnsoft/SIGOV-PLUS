using Sigov.Worker.Outbox;

namespace Sigov.Worker.Outbox.Handlers;

public sealed class RelatorioOutboxHandler : DefaultOutboxHandler
{
    public override bool CanHandle(string tipoEvento) => tipoEvento.Contains("Relatorio", StringComparison.OrdinalIgnoreCase) || string.Equals(tipoEvento, "RemessaOficialGerada", StringComparison.OrdinalIgnoreCase);
}
