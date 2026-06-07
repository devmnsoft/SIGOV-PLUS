namespace Sigov.Application.Core.Pessoas;

public sealed class PessoaPermissionChecker
{
    public bool CanViewFullPersonalData(IEnumerable<string> permissions) => permissions.Contains("core.pessoas.dados-completos", StringComparer.OrdinalIgnoreCase);
}
