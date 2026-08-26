using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sigov.Application.Abstractions;
using Sigov.Application.Authorization;
using Sigov.Application.Compras;

namespace Sigov.Web.Controllers;

[Authorize]
public sealed class ComprasLicitaProController(ILicitaProService service,ICurrentTenant tenant,ICurrentUser user,IAuthorizationEvaluator authorization) : Controller
{
    [HttpGet("/Compras/LicitaPro")]
    public async Task<IActionResult> Dashboard(CancellationToken ct)=>await ViewAllowed(LicitaProPermissoes.Dashboard,await service.DashboardAsync(T(),E(),ct),"Dashboard",ct);
    [HttpGet("/Compras/LicitaPro/Fontes")]
    public async Task<IActionResult> Fontes(CancellationToken ct)=>await ViewAllowed(LicitaProPermissoes.FonteVer,await service.FontesAsync(T(),E(),ct),"Fontes",ct);
    [HttpGet("/Compras/LicitaPro/Oportunidades")]
    public async Task<IActionResult> Oportunidades([FromQuery]LicitaProFiltro filtro,CancellationToken ct) { ViewBag.Fontes=await service.FontesAsync(T(),E(),ct); return await ViewAllowed(LicitaProPermissoes.OportunidadeVer,await service.OportunidadesAsync(T(),E(),filtro,ct),"Oportunidades",ct); }
    [HttpGet("/Compras/LicitaPro/Oportunidades/Nova")]
    public async Task<IActionResult> NovaOportunidade(CancellationToken ct) { await LoadFontes(ct); return await ViewAllowed(LicitaProPermissoes.OportunidadeGerir,new OportunidadeInput(),"OportunidadeForm",ct); }
    [HttpPost("/Compras/LicitaPro/Oportunidades/Nova"),ValidateAntiForgeryToken]
    public async Task<IActionResult> NovaOportunidade(OportunidadeInput input,CancellationToken ct) { if(!await Allowed(LicitaProPermissoes.OportunidadeGerir,ct))return Forbid(); if(!ModelState.IsValid){await LoadFontes(ct);return View("OportunidadeForm",input);} try{var id=await service.CriarOportunidadeAsync(T(),E(),U(),Trace(),input,ct);TempData["Success"]="Oportunidade salva com versionamento.";return Redirect($"/Compras/LicitaPro/Oportunidades/{id}");}catch(Exception ex)when(ex is ArgumentException or InvalidOperationException){ModelState.AddModelError("",ex.Message);await LoadFontes(ct);return View("OportunidadeForm",input);} }
    [HttpGet("/Compras/LicitaPro/Oportunidades/{id:long}")]
    public async Task<IActionResult> Detalhe(long id,CancellationToken ct) { var item=await service.OportunidadeAsync(T(),E(),id,ct); if(item is null)return NotFound(); ViewBag.Processos=(await service.WorkspaceAsync(T(),E(),"Portal",new(),ct)).Processos; return await ViewAllowed(LicitaProPermissoes.OportunidadeVer,item,"OportunidadeDetalhe",ct); }
    [HttpPost("/Compras/LicitaPro/Oportunidades/{id:long}/Vincular"),ValidateAntiForgeryToken]
    public async Task<IActionResult> Vincular(long id,VinculoOportunidadeInput input,CancellationToken ct) { if(!await Allowed(LicitaProPermissoes.OportunidadeGerir,ct))return Forbid(); if(!ModelState.IsValid){TempData["Error"]="Selecione um processo válido.";return Redirect($"/Compras/LicitaPro/Oportunidades/{id}");} try{await service.VincularAsync(T(),E(),U(),Trace(),id,input.ProcessoId!.Value,ct);TempData["Success"]="Oportunidade vinculada ao processo.";}catch(Exception ex)when(ex is ArgumentException or InvalidOperationException){TempData["Error"]=ex.Message;} return Redirect($"/Compras/LicitaPro/Oportunidades/{id}"); }
    [HttpGet("/Compras/LicitaPro/{area:regex(^(Importacoes|Portal|Documentos|Checklists|Analises|Agenda|Alertas|Auditoria)$)}")]
    public async Task<IActionResult> Workspace(string area,[FromQuery]LicitaProFiltro filtro,CancellationToken ct)=>await ViewAllowed(Permission(area),await service.WorkspaceAsync(T(),E(),area,filtro,ct),"Workspace",ct);
    [HttpPost("/Compras/LicitaPro/Documentos/Novo"),ValidateAntiForgeryToken]
    public async Task<IActionResult> Documento([Bind(Prefix="Documento")]DocumentoFornecedorInput input,CancellationToken ct)=>await Post(input,LicitaProPermissoes.DocumentoGerir,async()=>await service.CriarDocumentoAsync(T(),E(),U(),Trace(),input,ct),"Documentos",ct);
    [HttpPost("/Compras/LicitaPro/Agenda/Nova"),ValidateAntiForgeryToken]
    public async Task<IActionResult> Agenda([Bind(Prefix="Agenda")]AgendaPropostaInput input,CancellationToken ct)=>await Post(input,LicitaProPermissoes.AgendaGerir,async()=>await service.CriarAgendaAsync(T(),E(),U(),Trace(),input,ct),"Agenda",ct);
    [HttpGet("/Compras/LicitaPro/Relatorios/{area:regex(^(oportunidades|documentos|checklists|agenda|auditoria)$)}.csv")]
    public async Task<IActionResult> Csv(string area,[FromQuery]LicitaProFiltro filtro,CancellationToken ct)=>await Allowed(LicitaProPermissoes.Exportar,ct)?File(await service.ExportarAsync(T(),E(),U(),Trace(),area,filtro,ct),"text/csv; charset=utf-8",$"licitapro-{area}.csv"):Forbid();
    async Task<IActionResult> Post(object input,string permission,Func<Task<long>> action,string area,CancellationToken ct) { if(!await Allowed(permission,ct))return Forbid(); if(ModelState.IsValid)try{await action();TempData["Success"]="Registro salvo com sucesso.";return Redirect($"/Compras/LicitaPro/{area}");}catch(Exception ex)when(ex is ArgumentException or InvalidOperationException){ModelState.AddModelError("",ex.Message);} var model=await service.WorkspaceAsync(T(),E(),area,new(),ct);return View("Workspace",model); }
    async Task LoadFontes(CancellationToken ct)=>ViewBag.Fontes=await service.FontesAsync(T(),E(),ct);
    static string Permission(string area)=>area switch{"Importacoes"=>LicitaProPermissoes.FonteVer,"Portal"=>LicitaProPermissoes.PortalVer,"Documentos"=>LicitaProPermissoes.DocumentoVer,"Checklists"=>LicitaProPermissoes.ChecklistVer,"Analises"=>LicitaProPermissoes.AnaliseVer,"Agenda"=>LicitaProPermissoes.AgendaVer,"Auditoria"=>LicitaProPermissoes.AuditoriaVer,_=>LicitaProPermissoes.Dashboard};
    async Task<IActionResult> ViewAllowed(string p,object model,string view,CancellationToken ct)=>await Allowed(p,ct)?View(view,model):Forbid();
    async Task<bool> Allowed(string p,CancellationToken ct){var i=p.LastIndexOf('.');return(await authorization.EvaluateAsync(new(U(),"compras",p[..i],p[(i+1)..],T(),E(),tenant.ExercicioId,null,null,Trace(),"WEB_FUNC03_LICITAPRO"),ct)).Permitido;}
    long T()=>tenant.TenantId??throw new InvalidOperationException("tenant_id obrigatório."); long E()=>tenant.EntidadeId??throw new InvalidOperationException("entidade_id obrigatório."); long U()=>user.UsuarioId??throw new InvalidOperationException("Usuário obrigatório."); string Trace()=>HttpContext.TraceIdentifier;
}
