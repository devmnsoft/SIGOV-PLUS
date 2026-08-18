using Microsoft.AspNetCore.Authorization;using Microsoft.AspNetCore.Mvc;
namespace Sigov.Web.Controllers;
[Authorize,Route("Educacao/TransporteEscolar")] public sealed class EducacaoTransporteEscolarController:Controller{
[HttpGet("Dashboard")] public IActionResult Dashboard() => Pagina("Dashboard");
[HttpGet("Rotas")] public IActionResult Rotas() => Pagina("Rotas");
[HttpGet("RotaDetalhe")] public IActionResult RotaDetalhe() => Pagina("RotaDetalhe");
[HttpGet("Veiculos")] public IActionResult Veiculos() => Pagina("Veiculos");
[HttpGet("Motoristas")] public IActionResult Motoristas() => Pagina("Motoristas");
[HttpGet("Alunos")] public IActionResult Alunos() => Pagina("Alunos");
[HttpGet("Viagens")] public IActionResult Viagens() => Pagina("Viagens");
[HttpGet("Ocorrencias")] public IActionResult Ocorrencias() => Pagina("Ocorrencias");
[HttpGet("Custos")] public IActionResult Custos() => Pagina("Custos");
[HttpGet("Relatorios")] public IActionResult Relatorios() => Pagina("Relatorios");
private IActionResult Pagina(string pagina){ViewData["Modulo"]="TransporteEscolar";ViewData["Pagina"]=pagina;return View("~/Views/Educacao/Avancada.cshtml");}}