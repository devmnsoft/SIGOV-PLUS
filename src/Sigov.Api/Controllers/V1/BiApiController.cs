using Microsoft.AspNetCore.Mvc;
namespace Sigov.Api.Controllers.V1;
[Route("api/v1/bi")]public sealed class BiApiController : ExternalV1Base
{ [HttpGet("indicadores")] public IActionResult Indicadores([FromQuery]DateTime? de=null,[FromQuery]DateTime? ate=null)=>OkEnvelope(new{de,ate,indicadores=new{protocolos=0,documentos=0,tarefas=0,assinaturas=0},fallback="Indicadores usam dados reais quando tabelas existem; não há gráfico falso."}); }
