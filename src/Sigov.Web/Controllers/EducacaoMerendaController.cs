using Microsoft.AspNetCore.Authorization;using Microsoft.AspNetCore.Mvc;
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
private IActionResult Pagina(string pagina){ViewData["Modulo"]="Merenda";ViewData["Pagina"]=pagina;return View("~/Views/Educacao/Avancada.cshtml");}}