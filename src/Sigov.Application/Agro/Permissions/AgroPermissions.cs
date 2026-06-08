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
    public const string ProdutorVisualizar = "agro.produtor.visualizar";
    public const string ProdutorCriar = "agro.produtor.criar";
    public const string ProdutorEditar = "agro.produtor.editar";
    public const string ProdutorExcluir = "agro.produtor.excluir";
    public const string ProdutorVisualizarDadosCompletos = "agro.produtor.visualizar_dados_completos";
    public const string PropriedadeVisualizar = "agro.propriedade.visualizar";
    public const string PropriedadeCriar = "agro.propriedade.criar";
    public const string PropriedadeEditar = "agro.propriedade.editar";
    public const string PropriedadeExcluir = "agro.propriedade.excluir";
    public const string TalhaoVisualizar = "agro.talhao.visualizar";
    public const string TalhaoCriar = "agro.talhao.criar";
    public const string TalhaoEditar = "agro.talhao.editar";
    public const string CulturaVisualizar = "agro.cultura.visualizar";
    public const string CulturaCriar = "agro.cultura.criar";
    public const string CulturaEditar = "agro.cultura.editar";
    public const string SafraVisualizar = "agro.safra.visualizar";
    public const string SafraCriar = "agro.safra.criar";
    public const string SafraEditar = "agro.safra.editar";
    public const string ProducaoVisualizar = "agro.producao.visualizar";
    public const string ProducaoCriar = "agro.producao.criar";
    public const string ProducaoEditar = "agro.producao.editar";
    public const string ProducaoExcluir = "agro.producao.excluir";
}
