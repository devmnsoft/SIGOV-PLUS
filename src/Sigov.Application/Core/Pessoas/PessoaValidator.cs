namespace Sigov.Application.Core.Pessoas;

public sealed class PessoaValidator
{
    public bool IsNomeValido(string? nome) => !string.IsNullOrWhiteSpace(nome);

    public string NormalizeDocumento(string? documento) => new((documento ?? string.Empty).Where(char.IsDigit).ToArray());
}
