using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sigov.Application.Fiscalizacao;
using Sigov.Web.Models.Fiscalizacao;

namespace Sigov.Web.Controllers;

[Authorize,Route("Fiscalizacao")]
public sealed class FiscalizacaoController(IFiscalizacaoRepository repository):Controller
{
 [HttpGet(""),Authorize(Policy="FISCALIZACAO_DASHBOARD_VIEW")] public async Task<IActionResult> Index(string? modulo,string? status,DateOnly? inicio,DateOnly? fim,string? busca,CancellationToken ct)=>View(await repository.DashboardAsync(Context(),new(modulo,status,inicio,fim,busca),ct));
 [HttpGet("Ordens"),Authorize(Policy="FISCALIZACAO_ORDEM_VIEW")] public Task<IActionResult> Ordens(string? modulo,string? status,DateOnly? inicio,DateOnly? fim,string? busca,CancellationToken ct)=>Lista("Ordens","Ordens de fiscalização",modulo,status,inicio,fim,busca,ct);
 [HttpGet("Ordens/Create"),Authorize(Policy="FISCALIZACAO_ORDEM_MANAGE")] public async Task<IActionResult> Create(string? origem,CancellationToken ct)=>View("OrdemForm",await Form(null,new(){OrigemModulo=origem??"OBRAS"},ct));
 [HttpPost("Ordens/Create"),ValidateAntiForgeryToken,Authorize(Policy="FISCALIZACAO_ORDEM_MANAGE")] public async Task<IActionResult> Create(OrdemFiscalizacaoRequest ordem,CancellationToken ct)=>await Save(null,ordem,ct);
 [HttpGet("Ordens/Edit/{id:long}"),Authorize(Policy="FISCALIZACAO_ORDEM_MANAGE")] public async Task<IActionResult> Edit(long id,CancellationToken ct){var o=await repository.ObterOrdemAsync(Context(),id,ct);return o is null?NotFound():View("OrdemForm",await Form(id,o,ct));}
 [HttpPost("Ordens/Edit/{id:long}"),ValidateAntiForgeryToken,Authorize(Policy="FISCALIZACAO_ORDEM_MANAGE")] public Task<IActionResult> Edit(long id,OrdemFiscalizacaoRequest ordem,CancellationToken ct)=>Save(id,ordem,ct);
 [HttpGet("Ordens/Details/{id:long}"),Authorize(Policy="FISCALIZACAO_ORDEM_VIEW")] public async Task<IActionResult> Details(long id,CancellationToken ct){var o=await repository.ObterOrdemAsync(Context(),id,ct);return o is null?NotFound():View(o);}
 [HttpPost("Ordens/{id:long}/Cancelar"),ValidateAntiForgeryToken,Authorize(Policy="FISCALIZACAO_ORDEM_MANAGE")] public Task<IActionResult> Cancelar(long id,string? justificativa,CancellationToken ct)=>Transition(id,"CANCELADA",justificativa,ct);
 [HttpPost("Ordens/{id:long}/Concluir"),ValidateAntiForgeryToken,Authorize(Policy="FISCALIZACAO_ORDEM_MANAGE")] public Task<IActionResult> Concluir(long id,string? justificativa,CancellationToken ct)=>Transition(id,"CONCLUIDA",justificativa,ct);
 [HttpGet("Vistorias"),Authorize(Policy="FISCALIZACAO_VISTORIA_VIEW")] public Task<IActionResult> Vistorias(string? modulo,string? status,DateOnly? inicio,DateOnly? fim,string? busca,CancellationToken ct)=>Lista("Vistorias","Vistorias",modulo,status,inicio,fim,busca,ct);
 [HttpGet("Vistorias/Create"),Authorize(Policy="FISCALIZACAO_VISTORIA_MANAGE")] public IActionResult VistoriaCreate()=>View("Capability",("Nova vistoria","Selecione uma ordem elegível na lista de ordens; o vínculo nunca é digitado manualmente."));
 [HttpGet("Vistorias/Checklist"),Authorize(Policy="FISCALIZACAO_VISTORIA_MANAGE")] public IActionResult Checklist()=>View("Capability",("Checklist da vistoria","Itens obrigatórios são validados pelo serviço antes da conclusão."));
 [HttpGet("Vistorias/Details/{id:long}"),Authorize(Policy="FISCALIZACAO_VISTORIA_VIEW")] public IActionResult VistoriaDetails(long id)=>View("Capability",("Detalhes da vistoria",$"Vistoria selecionada: {id}."));
 [HttpGet("Checklists"),Authorize(Policy="FISCALIZACAO_CHECKLIST_MANAGE")] public IActionResult Checklists()=>View("Capability",("Modelos de checklist","Cadastre modelos e itens tipados; não existem catálogos simulados."));
 [HttpGet("Equipes"),Authorize(Policy="FISCALIZACAO_ORDEM_MANAGE")] public IActionResult Equipes()=>View("Capability",("Equipes de campo","Membros devem ser selecionados no diretório oficial de usuários."));
 [HttpGet("Roteiros"),Authorize(Policy="FISCALIZACAO_ORDEM_MANAGE")] public IActionResult Roteiros()=>View("Capability",("Roteiros de fiscalização","Ordens elegíveis são selecionadas por checkbox."));
 [HttpGet("Autos"),Authorize(Policy="FISCALIZACAO_AUTO_MANAGE")] public Task<IActionResult> Autos(string? modulo,string? status,DateOnly? inicio,DateOnly? fim,string? busca,CancellationToken ct)=>Lista("Autos","Autos e notificações",modulo,status,inicio,fim,busca,ct);
 [HttpGet("Evidencias"),Authorize(Policy="FISCALIZACAO_VISTORIA_VIEW")] public Task<IActionResult> Evidencias(string? modulo,string? status,DateOnly? inicio,DateOnly? fim,string? busca,CancellationToken ct)=>Lista("Evidencias","Evidências",modulo,status,inicio,fim,busca,ct);
 [HttpGet("Sincronizacao"),Authorize(Policy="FISCALIZACAO_SINCRONIZACAO_VIEW")] public Task<IActionResult> Sincronizacao(string? status,DateOnly? inicio,DateOnly? fim,string? busca,CancellationToken ct)=>Lista("Sincronizacao","Sincronização de campo",null,status,inicio,fim,busca,ct);
 [HttpGet("Relatorios"),Authorize(Policy="FISCALIZACAO_RELATORIO_EXPORT")] public IActionResult Relatorios()=>View();
 [HttpGet("Relatorios/{recurso}.csv"),Authorize(Policy="FISCALIZACAO_RELATORIO_EXPORT")] public async Task<IActionResult> Csv(string recurso,string? modulo,string? status,DateOnly? inicio,DateOnly? fim,string? busca,CancellationToken ct)=>File(await repository.CsvAsync(Context(),recurso,new(modulo,status,inicio,fim,busca),ct),"text/csv; charset=utf-8",$"fiscaliza360-{recurso}-{DateTime.UtcNow:yyyyMMdd}.csv");
 private async Task<IActionResult> Lista(string r,string title,string? m,string? s,DateOnly? i,DateOnly? f,string? b,CancellationToken ct){var filter=new FiscalizacaoFiltro(m,s,i,f,b);return View("Lista",new FiscalizacaoListaViewModel(title,r,await repository.ListarAsync(Context(),r,filter,ct),filter));}
 private async Task<IActionResult> Save(long? id,OrdemFiscalizacaoRequest o,CancellationToken ct){if(o.AgendadaEm<DateTimeOffset.UtcNow.AddMinutes(-5))ModelState.AddModelError(nameof(o.AgendadaEm),"O agendamento não pode estar no passado.");if(!ModelState.IsValid)return View("OrdemForm",await Form(id,o,ct));try{var saved=await repository.SalvarOrdemAsync(Context(),o,id,ct);return RedirectToAction(nameof(Details),new{id=saved});}catch(ArgumentException ex){ModelState.AddModelError("",ex.Message);return View("OrdemForm",await Form(id,o,ct));}}
 private async Task<FiscalizacaoOrdemFormViewModel> Form(long? id,OrdemFiscalizacaoRequest o,CancellationToken ct)=>new(id,o,await repository.OpcoesAsync(Context(),"Equipe",null,ct),await repository.OpcoesAsync(Context(),"Registro",o.OrigemModulo,ct));
 private async Task<IActionResult> Transition(long id,string status,string? reason,CancellationToken ct){try{await repository.TransicionarOrdemAsync(Context(),id,status,reason,ct);return RedirectToAction(nameof(Details),new{id});}catch(ArgumentException ex){TempData["Error"]=ex.Message;return RedirectToAction(nameof(Details),new{id});}}
 private FiscalizacaoContexto Context(){bool P(string claim,out long value)=>long.TryParse(User.FindFirst(claim)?.Value,out value)&&value>0;if(!P("tenant_id",out var t)||(!P("entidade_id",out var e)&&!P("entity_id",out e))||!P("exercicio_id",out var x)||(!P("user_id",out var u)&&!P("sub",out u)))throw new UnauthorizedAccessException("Contexto tenant/entidade/exercício/usuário não resolvido.");return new(t,e,x,u);}
}
