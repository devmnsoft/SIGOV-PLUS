using Microsoft.AspNetCore.Authorization;using Microsoft.AspNetCore.Mvc;using Sigov.Web.Services;
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
// Ciclo 2 Sprint 6 (BR-6.1/BR-6.6): area renderiza sua view ligada aos endpoints tipados e o
// acesso e governado pelo modulo 'educacao' do banco (regra 11); sem acesso = estado nomeado.
private IActionResult Pagina(string pagina){ViewData["Modulo"]="TransporteEscolar";ViewData["Pagina"]=pagina;if(!HttpContext.RequestServices.GetRequiredService<IMenuPermissionService>().CanSeeModule(User,"educacao"))return View("~/Views/Educacao/Avancada/SemAcesso.cshtml");return View("~/Views/Educacao/Avancada/TransporteEscolar.cshtml");}}