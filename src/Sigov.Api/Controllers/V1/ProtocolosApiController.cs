using Microsoft.AspNetCore.Mvc;
namespace Sigov.Api.Controllers.V1;
[Route("api/v1/protocolos")]public sealed class ProtocolosApiController : ExternalV1Base
{
    [HttpGet] public IActionResult Listar([FromQuery] int page=1,[FromQuery] int pageSize=20,[FromQuery] string? status=null,[FromQuery] DateTime? de=null,[FromQuery] DateTime? ate=null)=>OkEnvelope(new{page=Math.Max(1,page),pageSize=Math.Clamp(pageSize,1,100),status,de,ate,items=Array.Empty<object>()});
    [HttpPost] public IActionResult Criar([FromBody] object payload)=>OkEnvelope(new{protocoloId=(long?)null,fallback="Criação exige escopo protocolos.write e tabela sigov.protocolo disponível; nenhum protocolo foi simulado."});
    [HttpGet("{id:long}")] public IActionResult Obter(long id)=>OkEnvelope(new{id,fallback="Detalhe mascarado por LGPD; dados reais dependem de tenant/API key."});
    [HttpPost("{id:long}/tramitar")] public IActionResult Tramitar(long id,[FromBody] object payload)=>OkEnvelope(new{id,fallback="Tramitação auditável preparada; persistência real depende do workflow."});
}
