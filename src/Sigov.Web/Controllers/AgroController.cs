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
    public IActionResult Programas() => View();
    public IActionResult Beneficios() => View();
    public IActionResult ConcessoesBeneficios() => View();
    public IActionResult Insumos() => View();
    public IActionResult DistribuicaoInsumos() => View();
    public IActionResult PatrulhaMecanizada() => View();
    public IActionResult Maquinas() => View();
    public IActionResult MaquinaDetalhe(long id) { ViewData["MaquinaId"] = id; return View(); }
    public IActionResult Implementos() => View();
    public IActionResult AgendaMaquinas() => View();
    public IActionResult ServicosMaquina() => View();
    public IActionResult ServicoMaquinaDetalhe(long id) { ViewData["ServicoMaquinaId"] = id; return View(); }
}
