using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Sigov.Web.Controllers;

/// <summary>Compatibilidade de navegação: o inventário funcional pertence ao módulo Patrimônio.</summary>
[Authorize]
public sealed class InventarioController : Controller
{
    [HttpGet("/Inventario"), HttpGet("/Inventario/Campanhas"), HttpGet("/Inventario/Campanhas/Nova"), HttpGet("/Inventario/Divergencias"), HttpGet("/Inventario/Relatorios")]
    public IActionResult Index() => RedirectPermanent("/Patrimonio/Inventarios");

    [HttpGet("/Inventario/Campanhas/{id:long}"), HttpGet("/Inventario/Campanhas/{id:long}/Itens")]
    public IActionResult Campanha(long id) => RedirectPermanent($"/Patrimonio/Inventarios/{id}");
}
