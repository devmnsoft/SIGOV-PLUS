using Microsoft.AspNetCore.Mvc;
namespace Sigov.Api.Controllers.V1;
[Route("api/v1/tarefas")]public sealed class TarefasApiController : ExternalV1Base
{ [HttpGet] public IActionResult Listar([FromQuery]int page=1,[FromQuery]int pageSize=20,[FromQuery]string? status=null)=>OkEnvelope(new{page,pageSize=Math.Clamp(pageSize,1,100),status,items=Array.Empty<object>()}); [HttpPost] public IActionResult Criar([FromBody]object payload)=>OkEnvelope(new{fallback="Criação exige escopo tarefas.write; sem simulação de persistência."}); [HttpPost("{id:long}/concluir")] public IActionResult Concluir(long id)=>OkEnvelope(new{id,fallback="Conclusão preparada para auditoria e evento tarefa.concluida."}); }
