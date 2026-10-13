using Microsoft.AspNetCore.Authorization;using Microsoft.AspNetCore.Mvc;using Sigov.Web.Services;
namespace Sigov.Web.Controllers;
[Authorize,Route("Educacao/Merenda")] public sealed class EducacaoMerendaController:Controller{
[HttpGet("Dashboard")] public IActionResult Dashboard() => Pagina("Dashboard");
[HttpGet("Produtos")] public IActionResult Produtos() => Pagina("Produtos");
[HttpGet("Fornecedores")] public IActionResult Fornecedores() => Pagina("Fornecedores");
[HttpGet("Estoque")] public IActionResult Estoque() => Pagina("Estoque");
[HttpGet("Movimentos")] public IActionResult Movimentos() => Pagina("Movimentos");
[HttpGet("Cardapios")] public IActionResult Cardapios() => Pagina("Cardapios");
[HttpGet("Distribuicoes")] public IActionResult Distribuicoes() => Pagina("Distribuicoes");
[HttpGet("Requisicoes")] public IActionResult Requisicoes() => Pagina("Requisicoes");
[HttpGet("Consumo")] public IActionResult Consumo() => Pagina("Consumo");
[HttpGet("Relatorios")] public IActionResult Relatorios() => Pagina("Relatorios");
// Ciclo 2 Sprint 6 (BR-6.1/BR-6.6): cada area renderiza sua view ligada aos endpoints tipados
// (sem a fachada estatica) e o acesso e governado pelo modulo 'educacao' do banco (regra 11);
// perfil sem acesso ve o estado nomeado 'sem acesso' em vez de 500/fachada.
private IActionResult Pagina(string pagina){ViewData["Modulo"]="Merenda";ViewData["Pagina"]=pagina;if(!HttpContext.RequestServices.GetRequiredService<IMenuPermissionService>().CanSeeModule(User,"educacao"))return View("~/Views/Educacao/Avancada/SemAcesso.cshtml");return View("~/Views/Educacao/Avancada/Merenda.cshtml");}}