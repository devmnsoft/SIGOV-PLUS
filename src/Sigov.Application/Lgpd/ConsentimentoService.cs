namespace Sigov.Application.Lgpd;

public sealed class ConsentimentoService
{
    public bool IsBaseAtiva(bool revogado) => !revogado;
}
