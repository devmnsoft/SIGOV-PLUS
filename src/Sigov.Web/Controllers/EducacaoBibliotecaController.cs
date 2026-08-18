using Microsoft.AspNetCore.Authorization;using Microsoft.AspNetCore.Mvc;
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
private IActionResult Pagina(string pagina){ViewData["Modulo"]="Biblioteca";ViewData["Pagina"]=pagina;return View("~/Views/Educacao/Avancada.cshtml");}}