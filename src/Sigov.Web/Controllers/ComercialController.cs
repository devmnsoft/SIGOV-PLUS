using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace Sigov.Web.Controllers;
[Authorize]
public sealed class ComercialController : Controller
{
 [Route("/Comercial"),Route("/Comercial/Dashboard")] public IActionResult Dashboard()=>View("~/Views/Comercial/Dashboard.cshtml");
 [Route("/Comercial/Clientes")] public IActionResult Clientes()=>View("~/Views/Comercial/Listagem.cshtml",new CommercialPage("Clientes","/api/comercial/clientes","Novo cliente"));
 [Route("/Comercial/Leads")] public IActionResult Leads()=>View("~/Views/Comercial/Listagem.cshtml",new CommercialPage("Leads","/api/comercial/leads","Novo lead"));
 [Route("/Comercial/Oportunidades"),Route("/Comercial/Funil")] public IActionResult Funil()=>View("~/Views/Comercial/Funil.cshtml");
 [Route("/Comercial/Propostas")] public IActionResult Propostas()=>View("~/Views/Comercial/Listagem.cshtml",new CommercialPage("Propostas","/api/comercial/propostas","Nova proposta"));
 [Route("/Comercial/Propostas/{id:guid}/Imprimir")] public IActionResult Imprimir(Guid id)=>View("~/Views/Comercial/Impressao.cshtml",id);
 [Route("/Comercial/Pedidos")] public IActionResult Pedidos()=>View("~/Views/Comercial/Listagem.cshtml",new CommercialPage("Pedidos","/api/comercial/pedidos",null));
 [Route("/Comercial/TabelasPreco")] public IActionResult TabelasPreco()=>View("~/Views/Comercial/Indisponivel.cshtml", "Tabelas de preço serão integradas ao catálogo persistido; nenhum dado demonstrativo é exibido.");
}
public sealed record CommercialPage(string Title,string Endpoint,string? CreateLabel);
