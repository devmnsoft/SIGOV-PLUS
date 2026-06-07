namespace Sigov.Application.Lgpd;

public sealed class RelatorioTitularService
{
    public string GerarNumero(long pessoaId, DateTime data) => $"LGPD-{data:yyyy}-{pessoaId:000000}";
}
