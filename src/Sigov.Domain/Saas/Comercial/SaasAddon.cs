using Sigov.Domain.Common;

namespace Sigov.Domain.Saas.Comercial;

public sealed class SaasAddon : Entity
{
    public SaasAddon(long id, string codigo, string nome, SaasAddonTipo tipoAddon, decimal? preco)
    {
        Id = id;
        Codigo = codigo?.Trim().ToUpperInvariant() ?? string.Empty;
        Nome = nome?.Trim() ?? string.Empty;
        TipoAddon = tipoAddon;
        Preco = preco;
    }

    public string Codigo { get; }
    public string Nome { get; }
    public SaasAddonTipo TipoAddon { get; }
    public decimal? Preco { get; }

    public Result Validate()
    {
        if (string.IsNullOrWhiteSpace(Codigo) || string.IsNullOrWhiteSpace(Nome)) return Result.Failure("Addon exige código e nome.");
        return Preco is < 0 ? Result.Failure("Preço do addon não pode ser negativo.") : Result.Success();
    }
}
