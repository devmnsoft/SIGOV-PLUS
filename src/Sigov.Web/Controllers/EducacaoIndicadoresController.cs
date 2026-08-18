using Microsoft.AspNetCore.Authorization;using Microsoft.AspNetCore.Mvc;
namespace Sigov.Web.Controllers;
[Authorize,Route("Educacao/Indicadores")] public sealed class EducacaoIndicadoresController:Controller{
[HttpGet("Dashboard")] public IActionResult Dashboard() => Pagina("Dashboard");
[HttpGet("Fundeb")] public IActionResult Fundeb() => Pagina("Fundeb");
[HttpGet("Custos")] public IActionResult Custos() => Pagina("Custos");
[HttpGet("Rateios")] public IActionResult Rateios() => Pagina("Rateios");
[HttpGet("ProjecoesMatricula")] public IActionResult ProjecoesMatricula() => Pagina("ProjecoesMatricula");
[HttpGet("Educacenso")] public IActionResult Educacenso() => Pagina("Educacenso");
[HttpGet("EducacensoInconsistencias")] public IActionResult EducacensoInconsistencias() => Pagina("EducacensoInconsistencias");
[HttpGet("AvaliacaoMagisterio")] public IActionResult AvaliacaoMagisterio() => Pagina("AvaliacaoMagisterio");
[HttpGet("SerieHistorica")] public IActionResult SerieHistorica() => Pagina("SerieHistorica");
[HttpGet("Relatorios")] public IActionResult Relatorios() => Pagina("Relatorios");
private IActionResult Pagina(string pagina){ViewData["Modulo"]="Indicadores";ViewData["Pagina"]=pagina;return View("~/Views/Educacao/Avancada.cshtml");}}