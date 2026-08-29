using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sigov.Application.Royalties;
namespace Sigov.Web.Controllers;
[AllowAnonymous,Route("Transparencia/Royalties")]
public sealed class RoyaltiesTransparenciaController(IRoyaltiesRepository repository):Controller
{
 [HttpGet("")] public async Task<IActionResult> Index(string? busca,DateOnly? inicio,DateOnly? fim,CancellationToken ct){if(!long.TryParse(HttpContext.Request.Query["tenant"],out var tenant)||tenant<=0||!long.TryParse(HttpContext.Request.Query["entidade"],out var entidade)||entidade<=0)return BadRequest("O contexto público da entidade não foi resolvido.");ViewData["Publico"]=true;ViewData["Titulo"]="Transparência de royalties";ViewData["Recurso"]="transparencia";return View("~/Views/Royalties/Lista.cshtml",await repository.ListarAsync(new(tenant,entidade,null,HttpContext.TraceIdentifier),"transparencia",new(busca,"PUBLICADO",null,null,null,inicio,fim),ct));}
}
