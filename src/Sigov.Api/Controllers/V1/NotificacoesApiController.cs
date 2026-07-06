using Microsoft.AspNetCore.Mvc;
namespace Sigov.Api.Controllers.V1;
[Route("api/v1/notificacoes")]public sealed class NotificacoesApiController : ExternalV1Base
{ [HttpGet] public IActionResult Listar([FromQuery]int page=1,[FromQuery]int pageSize=20)=>OkEnvelope(new{page,pageSize=Math.Clamp(pageSize,1,100),items=Array.Empty<object>()}); [HttpPost("{id:long}/marcar-lida")] public IActionResult MarcarLida(long id)=>OkEnvelope(new{id,fallback="Marcação preparada; exige tenant e usuário/API key autorizados."}); }
