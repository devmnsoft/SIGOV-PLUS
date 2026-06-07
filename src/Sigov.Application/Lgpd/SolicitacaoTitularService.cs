namespace Sigov.Application.Lgpd;

public sealed class SolicitacaoTitularService
{
    public DateTime CalcularPrazo(DateTime abertura) => abertura.AddDays(15);
}
