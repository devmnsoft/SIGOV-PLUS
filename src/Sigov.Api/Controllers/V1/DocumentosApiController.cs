using Microsoft.AspNetCore.Mvc;
namespace Sigov.Api.Controllers.V1;
[Route("api/v1/documentos")]public sealed class DocumentosApiController : ExternalV1Base
{ [HttpGet] public IActionResult Listar([FromQuery]int page=1,[FromQuery]int pageSize=20,[FromQuery]string? status=null)=>OkEnvelope(new{page,pageSize=Math.Clamp(pageSize,1,100),status,items=Array.Empty<object>()}); [HttpPost] public IActionResult Criar([FromBody]object payload)=>OkEnvelope(new{fallback="Upload/metadados exigem storage e sigov.ged_documento; nenhum documento foi simulado."}); [HttpGet("{id:long}")] public IActionResult Obter(long id)=>OkEnvelope(new{id,publico=false,fallback="Conteúdo sigiloso não é exposto por API pública."}); }
