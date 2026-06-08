using Sigov.Domain.Common;

namespace Sigov.Domain.Saas.Comercial;

public sealed class SaasPlano : Entity
{
    public SaasPlano(long id, string codigo, string nome, string? descricao, bool publico, SaasPlanoTipo tipoPlano, decimal? precoBase, SaasPeriodicidade periodicidade, int? limiteUsuarios, bool permiteWhiteLabel, bool permiteDominioCustomizado)
    {
        Id = id;
        Codigo = codigo?.Trim().ToUpperInvariant() ?? string.Empty;
        Nome = nome?.Trim() ?? string.Empty;
        Descricao = descricao?.Trim();
        Publico = publico;
        TipoPlano = tipoPlano;
        PrecoBase = precoBase;
        Periodicidade = periodicidade;
        LimiteUsuarios = limiteUsuarios;
        PermiteWhiteLabel = permiteWhiteLabel;
        PermiteDominioCustomizado = permiteDominioCustomizado;
    }

    public string Codigo { get; }
    public string Nome { get; }
    public string? Descricao { get; }
    public bool Publico { get; }
    public SaasPlanoTipo TipoPlano { get; }
    public decimal? PrecoBase { get; }
    public SaasPeriodicidade Periodicidade { get; }
    public int? LimiteUsuarios { get; }
    public bool PermiteWhiteLabel { get; }
    public bool PermiteDominioCustomizado { get; }

    public Result Validate()
    {
        var errors = new List<ValidationError>();
        if (string.IsNullOrWhiteSpace(Codigo)) errors.Add(new ValidationError(nameof(Codigo), "Código do plano é obrigatório."));
        if (string.IsNullOrWhiteSpace(Nome)) errors.Add(new ValidationError(nameof(Nome), "Nome do plano é obrigatório."));
        if (Publico && string.IsNullOrWhiteSpace(Descricao)) errors.Add(new ValidationError(nameof(Descricao), "Plano público deve possuir descrição."));
        if (PrecoBase is < 0) errors.Add(new ValidationError(nameof(PrecoBase), "Preço não pode ser negativo."));
        if (LimiteUsuarios is < 0) errors.Add(new ValidationError(nameof(LimiteUsuarios), "Limite de usuários não pode ser negativo."));
        return errors.Count == 0 ? Result.Success() : Result.ValidationFailure(errors);
    }
}
