namespace Sigov.Application.Saas.Modules;

public sealed record ModulePackageItem(string Codigo, string Nome, string Descricao, IReadOnlyCollection<string> Modulos);
