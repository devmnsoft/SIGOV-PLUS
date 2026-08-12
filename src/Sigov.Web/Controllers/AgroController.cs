using Microsoft.AspNetCore.Mvc;

namespace Sigov.Web.Controllers;

public sealed class AgroController : Controller
{
    public IActionResult Dashboard() => View();
    public IActionResult MapaRural() => View();
    public IActionResult CamadasGeo() => View();
    public IActionResult Produtores() => View();
    public IActionResult ProdutorDetalhe(long id) { ViewData["ProdutorId"] = id; return View(); }
    public IActionResult Propriedades() => View();
    public IActionResult PropriedadeDetalhe(long id) { ViewData["PropriedadeId"] = id; return View(); }
    public IActionResult Talhoes() => View();
    public IActionResult Culturas() => View();
    public IActionResult Safras() => View();
    public IActionResult Producao() => View();
    [HttpGet("/Agro/Programas")]
    public IActionResult Programas() => View();
    public IActionResult Beneficios() => View();
    public IActionResult ConcessoesBeneficios() => View();
    public IActionResult Insumos() => View();
    public IActionResult DistribuicaoInsumos() => View();
    public IActionResult PatrulhaMecanizada() => View();
    [HttpGet("/Agro/Patrulha")]
    public IActionResult Patrulha() => View("PatrulhaMecanizada");
    public IActionResult Maquinas() => View();
    public IActionResult MaquinaDetalhe(long id) { ViewData["MaquinaId"] = id; return View(); }
    public IActionResult Implementos() => View();
    public IActionResult AgendaMaquinas() => View();
    public IActionResult ServicosMaquina() => View();
    public IActionResult ServicoMaquinaDetalhe(long id) { ViewData["ServicoMaquinaId"] = id; return View(); }
    public IActionResult EstradasVicinais() => View();
    public IActionResult EstradaVicinalDetalhe(long id) { ViewData["EstradaVicinalId"] = id; return View(); }
    public IActionResult PontosCriticos() => View();
    public IActionResult OcorrenciasRurais() => View();
    public IActionResult ManutencoesRurais() => View();
    public IActionResult Feiras() => View();
    public IActionResult FeiraDetalhe(long id) { ViewData["FeiraId"] = id; return View(); }
    public IActionResult Feirantes() => View();
    public IActionResult Agroindustrias() => View();
    public IActionResult AgroindustriaDetalhe(long id) { ViewData["AgroindustriaId"] = id; return View(); }
    public IActionResult InspecoesMunicipais() => View();
    public IActionResult ComprasAgriculturaFamiliar() => View();
    public IActionResult Bi() => View();
    public IActionResult Indicadores() => View();
    public IActionResult Relatorios() => View();
    public IActionResult ExecutarRelatorio(long id) { ViewData["ModeloId"] = id; return View(); }
    public IActionResult Transparencia() => View();
    public IActionResult Datasets() => View();
    public IActionResult DicionarioDados() => View();
    public IActionResult PainelComercial() => View();
}
