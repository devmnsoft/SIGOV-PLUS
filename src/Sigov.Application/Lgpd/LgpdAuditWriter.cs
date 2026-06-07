namespace Sigov.Application.Lgpd;

public sealed class LgpdAuditWriter
{
    public string BuildSolicitacaoKey(string numero) => $"sigov.lgpd_solicitacao:{numero}";
}
