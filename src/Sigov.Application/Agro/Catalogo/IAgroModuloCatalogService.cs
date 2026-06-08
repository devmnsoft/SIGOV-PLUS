using Sigov.Domain.Common;

namespace Sigov.Application.Agro.Catalogo;

public sealed record AgroModuloCatalogItem(string Codigo, string Nome, string RotaBase, IReadOnlyCollection<string> Permissoes, IReadOnlyCollection<string> FeatureFlags);

public interface IAgroModuloCatalogService
{
    Result<AgroModuloCatalogItem> ObterModuloAgro();
}
