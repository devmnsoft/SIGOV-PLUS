using Microsoft.AspNetCore.Authorization;using Microsoft.AspNetCore.Mvc;using Sigov.Web.Services;
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
// Ciclo 2 Sprint 6 (BR-6.1/BR-6.6): area renderiza sua view ligada aos endpoints tipados e o
// acesso e governado pelo modulo 'educacao' do banco (regra 11); sem acesso = estado nomeado.
private IActionResult Pagina(string pagina){ViewData["Modulo"]="Indicadores";ViewData["Pagina"]=pagina;if(!HttpContext.RequestServices.GetRequiredService<IMenuPermissionService>().CanSeeModule(User,"educacao"))return View("~/Views/Educacao/Avancada/SemAcesso.cshtml");return View("~/Views/Educacao/Avancada/Indicadores.cshtml");}}