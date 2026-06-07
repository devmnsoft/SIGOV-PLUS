namespace Sigov.Application.Lgpd;

public sealed class IncidenteSegurancaService
{
    public bool RequerComunicacao(string severidade) => string.Equals(severidade, "ALTA", StringComparison.OrdinalIgnoreCase) || string.Equals(severidade, "CRITICA", StringComparison.OrdinalIgnoreCase);
}
