namespace Sigov.Application.Lgpd;

public sealed class LgpdValidator
{
    public bool IsDescricaoIncidenteValida(string? descricao) => !string.IsNullOrWhiteSpace(descricao);
}
