namespace Sigov.Web.Models.PostBuild;

public sealed class ImplementationModuleViewModel
{
    public string Codigo { get; init; } = string.Empty;
    public string Titulo { get; init; } = "Módulo";
    public string Descricao { get; init; } = "Funcionalidade em implantação controlada no SIGOV PLUS.";
    public string Status { get; init; } = "Em implantação";
    public IReadOnlyCollection<string> ProximosPassos { get; init; } = Array.Empty<string>();
    public IReadOnlyCollection<string> ModulosRelacionados { get; init; } = Array.Empty<string>();
}
