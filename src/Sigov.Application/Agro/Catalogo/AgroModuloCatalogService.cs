using Microsoft.Extensions.Logging;
using Sigov.Application.Agro.Permissions;
using Sigov.Domain.Agro;
using Sigov.Domain.Common;

namespace Sigov.Application.Agro.Catalogo;

public sealed class AgroModuloCatalogService : IAgroModuloCatalogService
{
    private static readonly string[] Permissoes =
    {
        AgroPermissions.DashboardVisualizar,
        AgroPermissions.GeoVisualizar,
        AgroPermissions.GeoCriar,
        AgroPermissions.GeoEditar,
        AgroPermissions.GeoExcluir,
        AgroPermissions.GeoExportar
    };

    private static readonly string[] FeatureFlags =
    {
        "agro.dashboard",
        "agro.geo",
        "agro.exportacao_geojson"
    };

    private readonly ILogger<AgroModuloCatalogService> _logger;

    public AgroModuloCatalogService(ILogger<AgroModuloCatalogService> logger) => _logger = logger;

    public Result<AgroModuloCatalogItem> ObterModuloAgro()
    {
        _logger.LogDebug("Catálogo Agro solicitado.");
        return Result<AgroModuloCatalogItem>.Success(new AgroModuloCatalogItem(AgroModulo.Codigo, AgroModulo.Nome, AgroModulo.RotaBase, Permissoes, FeatureFlags));
    }
}
