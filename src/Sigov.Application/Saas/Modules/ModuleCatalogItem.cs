namespace Sigov.Application.Saas.Modules;

public sealed record ModuleCatalogItem(
    string Codigo,
    string Nome,
    string Descricao,
    string Categoria,
    bool VendidoSeparadamente,
    bool PodeIntegrarComOutros,
    IReadOnlyCollection<string> Dependencias,
    IReadOnlyCollection<ModuleFeatureItem> Funcionalidades,
    IReadOnlyCollection<string> Beneficios,
    string RotaPrincipal,
    IReadOnlyCollection<string> PermissoesBase);
