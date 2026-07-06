using Microsoft.AspNetCore.Mvc;
namespace Sigov.Api.Controllers.V1;
[Route("api/v1/fluxos")]public sealed class FluxosApiController : ExternalV1Base
{ [HttpGet] public IActionResult Listar([FromQuery]int page=1,[FromQuery]int pageSize=20)=>OkEnvelope(new{page,pageSize=Math.Clamp(pageSize,1,100),items=Array.Empty<object>(),eventos=new[]{"protocolo.criado","documento.assinado","tarefa.concluida"}}); [HttpGet("{id:long}")] public IActionResult Obter(long id)=>OkEnvelope(new{id,fallback="Fluxo ponta a ponta preparado para consulta operacional."}); }
