using Microsoft.AspNetCore.Mvc;
namespace Sigov.Api.Controllers.V1;
[Route("api/v1/assinaturas")]public sealed class AssinaturasApiController : ExternalV1Base
{ [HttpGet] public IActionResult Listar([FromQuery]int page=1,[FromQuery]int pageSize=20)=>OkEnvelope(new{page,pageSize=Math.Clamp(pageSize,1,100),items=Array.Empty<object>()}); [HttpPost] public IActionResult Criar([FromBody]object payload)=>OkEnvelope(new{tipo="eletronica_simples",fallback="Não simula ICP-Brasil; provider Gov.br/ICP deve ser configurado."}); [HttpGet("{id:long}")] public IActionResult Obter(long id)=>OkEnvelope(new{id,status="indisponivel_sem_schema"}); }
