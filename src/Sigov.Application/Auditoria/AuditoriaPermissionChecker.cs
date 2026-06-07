namespace Sigov.Application.Auditoria;

public sealed class AuditoriaPermissionChecker
{
    public bool CanViewAudit(IEnumerable<string> permissions) => permissions.Contains("auditoria.trilhas.visualizar", StringComparer.OrdinalIgnoreCase);
}
