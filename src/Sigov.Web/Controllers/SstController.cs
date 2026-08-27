using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Sigov.Application.Sst;
using Sigov.Web.Models.Sst;

namespace Sigov.Web.Controllers;
[Authorize]
[Route("SST")]
public sealed class SstController(SstService service) : Controller
{
 [HttpGet("")][HttpGet("Dashboard")] public async Task<IActionResult> Dashboard(CancellationToken ct)=>View(await service.DashboardAsync(ct));
 [HttpGet("ASO")] public async Task<IActionResult> Aso(CancellationToken ct)=>View(new SstAsoIndex(await service.ListarAsosAsync(ct)));
 [HttpGet("ASO/Create")] public async Task<IActionResult> AsoCreate(CancellationToken ct)=>View("AsoForm",await Load(new(),ct));
 [HttpPost("ASO/Create")][ValidateAntiForgeryToken] public async Task<IActionResult> AsoCreate(SstAsoForm model,CancellationToken ct)=>await Save(model,ct);
 [HttpGet("ASO/Edit/{id:long}")] public async Task<IActionResult> AsoEdit(long id,CancellationToken ct){var x=await service.ObterAsoAsync(id,ct);if(x is null)return NotFound();return View("AsoForm",await Load(new(){Id=x.Id,ServidorId=x.ServidorId,Tipo=x.Tipo,DataAso=x.DataAso,Medico=x.Medico,Resultado=x.Resultado,Restricao=x.Restricao,Validade=x.Validade},ct));}
 [HttpPost("ASO/Edit/{id:long}")][ValidateAntiForgeryToken] public async Task<IActionResult> AsoEdit(long id,SstAsoForm model,CancellationToken ct){model.Id=id;return await Save(model,ct);}
 [HttpGet("ASO/Details/{id:long}")] public async Task<IActionResult> AsoDetails(long id,CancellationToken ct){var x=await service.ObterAsoAsync(id,ct);return x is null?NotFound():View(x);}
 [HttpGet("{section}")] public IActionResult Module(string section)=>View("Module",Page(section));
 [HttpGet("{section}/{subsection}")] public IActionResult Submodule(string section,string subsection)=>View("Module",Page($"{section}/{subsection}"));
 private async Task<IActionResult> Save(SstAsoForm m,CancellationToken ct){if(m.Resultado=="apto_com_restricao"&&string.IsNullOrWhiteSpace(m.Restricao))ModelState.AddModelError(nameof(m.Restricao),"A descrição é obrigatória para aptidão com restrição.");if(!ModelState.IsValid)return View("AsoForm",await Load(m,ct));try{await service.SalvarAsoAsync(m.Id,new(m.ServidorId,m.Tipo,m.DataAso,m.Medico,m.Resultado,m.Restricao,m.Validade),ct);TempData["Success"]="ASO salvo e auditado com sucesso.";return RedirectToAction(nameof(Aso));}catch(ArgumentException e){ModelState.AddModelError(string.Empty,e.Message);return View("AsoForm",await Load(m,ct));}}
 private async Task<SstAsoForm> Load(SstAsoForm m,CancellationToken ct){m.Servidores=(await service.ServidoresAsync(ct)).Select(x=>new SelectListItem(x.Label,x.Id.ToString(),x.Id==m.ServidorId)).ToArray();return m;}
 private static SstModulePage Page(string key)=>key.ToLowerInvariant() switch {"ambientes"=>new("Ambientes de trabalho","Unidades e ambientes vinculados ao contexto organizacional.","Ambiente"),"riscos"=>new("Fatores de risco","Riscos físicos, químicos, biológicos, ergonômicos, de acidente e psicossociais.","Risco"),"exposicoes"=>new("Grupos e exposições","Exposições vigentes por servidor, cargo, unidade e ambiente.","Exposição"),"pgr"=>new("PGR","Programa de Gerenciamento de Riscos com aprovação e versionamento.","Programa"),"pcmso"=>new("PCMSO","Programa de Controle Médico de Saúde Ocupacional.","Programa"),"ltcat"=>new("LTCAT","Laudos técnicos e vigências controladas.","Laudo"),"exames"=>new("Exames ocupacionais","Agenda e situação dos exames integrados ao RH.","Saúde"),"epis"=>new("EPIs","Catálogo, CA, validade e integração com estoque/Ativos360.","Proteção"),"epis/entregas"=>new("Entregas de EPI","Entregas por servidor e responsável.","Entrega"),"epis/devolucoes"=>new("Devoluções de EPI","Motivo e estado do equipamento devolvido.","Devolução"),"treinamentos"=>new("Treinamentos SST","Capacitações, validade e carga horária.","Capacitação"),"treinamentos/participantes"=>new("Participantes","Presença e emissão controlada de certificados.","Participante"),"cat"=>new("CAT","Comunicações de Acidente do Trabalho.","CAT"),"acidentes"=>new("Acidentes de trabalho","Ocorrências e vínculo com afastamentos do RH.","Acidente"),"acidentes/investigacao"=>new("Investigações","Conclusões e ações corretivas.","Investigação"),"esocial"=>new("eSocial SST","Monitor dos eventos S-2210, S-2220, S-2240 e S-2230.","eSocial"),"esocial/eventos"=>new("Eventos eSocial","Eventos aceitos são imutáveis; pendências aguardam integração real.","Evento"),"esocial/remessas"=>new("Remessas eSocial","Remessas oficiais integradas, sem simulação de envio.","Remessa"),"esocial/inconsistencias"=>new("Inconsistências eSocial","Erros sanitizados e reenvio idempotente.","Validação"),_=>new("SST360","Saúde e Segurança do Trabalho integrada ao RH.","SST")};
}
