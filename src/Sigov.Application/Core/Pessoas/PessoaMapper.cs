namespace Sigov.Application.Core.Pessoas;

public sealed class PessoaMapper
{
    public string MapTipoPessoa(string tipoPessoa) => string.Equals(tipoPessoa, "J", StringComparison.OrdinalIgnoreCase) ? "Jurídica" : "Física";
}
