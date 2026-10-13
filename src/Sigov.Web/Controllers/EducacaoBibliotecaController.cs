using Microsoft.AspNetCore.Authorization;using Microsoft.AspNetCore.Mvc;using Sigov.Web.Services;
namespace Sigov.Web.Controllers;
[Authorize,Route("Educacao/Biblioteca")] public sealed class EducacaoBibliotecaController:Controller{
[HttpGet("Dashboard")] public IActionResult Dashboard() => Pagina("Dashboard");
[HttpGet("Acervo")] public IActionResult Acervo() => Pagina("Acervo");
[HttpGet("AcervoDetalhe")] public IActionResult AcervoDetalhe() => Pagina("AcervoDetalhe");
[HttpGet("Exemplares")] public IActionResult Exemplares() => Pagina("Exemplares");
[HttpGet("Emprestimos")] public IActionResult Emprestimos() => Pagina("Emprestimos");
[HttpGet("Reservas")] public IActionResult Reservas() => Pagina("Reservas");
[HttpGet("Atrasos")] public IActionResult Atrasos() => Pagina("Atrasos");
[HttpGet("Digital")] public IActionResult Digital() => Pagina("Digital");
[HttpGet("Relatorios")] public IActionResult Relatorios() => Pagina("Relatorios");
// Ciclo 2 Sprint 6 (BR-6.1/BR-6.6): area renderiza sua view ligada aos endpoints tipados e o
// acesso e governado pelo modulo 'educacao' do banco (regra 11); sem acesso = estado nomeado.
private IActionResult Pagina(string pagina){ViewData["Modulo"]="Biblioteca";ViewData["Pagina"]=pagina;if(!HttpContext.RequestServices.GetRequiredService<IMenuPermissionService>().CanSeeModule(User,"educacao"))return View("~/Views/Educacao/Avancada/SemAcesso.cshtml");return View("~/Views/Educacao/Avancada/Biblioteca.cshtml");}}