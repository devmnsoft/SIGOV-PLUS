namespace Sigov.Application.Auditoria;

public sealed class AcessoDadoPessoalService
{
    public string BuildFinalidade(string recurso) => $"Consulta LGPD de {recurso}";
}
