using Sigov.Domain.Agro;

namespace Sigov.Application.Agro.Permissions;

public static class AgroPermissions
{
    public const string Modulo = AgroModulo.Codigo;
    public const string DashboardVisualizar = AgroPermissao.DashboardVisualizar;
    public const string GeoVisualizar = AgroPermissao.GeoVisualizar;
    public const string GeoCriar = AgroPermissao.GeoCriar;
    public const string GeoEditar = AgroPermissao.GeoEditar;
    public const string GeoExcluir = AgroPermissao.GeoExcluir;
    public const string GeoExportar = AgroPermissao.GeoExportar;
}
