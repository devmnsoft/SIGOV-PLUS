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
}
