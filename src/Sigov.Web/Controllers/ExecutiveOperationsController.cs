using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sigov.Application.Abstractions;
using Sigov.Application.ExecutiveOperations;

namespace Sigov.Web.Controllers;

[Authorize]
public sealed class ExecutiveOperationsController(IGovernancaOperacionalService governance, IExecutiveOperationsRepository repository, ICurrentTenant tenant, ICurrentUser user) : Controller
{
    [HttpGet("/GovernancaOperacional")] public Task<IActionResult> Governance(CancellationToken ct)=>Dashboard("Governança Operacional","Visão unificada de riscos e rotinas de Educação, RH, Folha e Workflows.",null,ct);
    [HttpGet("/IntegracoesInternas")] public Task<IActionResult> Integrations(CancellationToken ct)=>Dashboard("Integrações Internas","Eventos auditáveis entre módulos — sem alegação de homologação externa.",null,ct);
    [HttpGet("/QualidadeDados")] public Task<IActionResult> Quality(CancellationToken ct)=>Dashboard("Qualidade de Dados","Inconsistências reais, priorizadas e protegidas pela LGPD.",null,ct);
    [HttpGet("/BuscaGlobal")] public Task<IActionResult> Search(CancellationToken ct)=>Dashboard("Busca Global","Localize módulos e rotinas autorizadas.",null,ct);
    [HttpGet("/Favoritos")] public Task<IActionResult> Favorites(CancellationToken ct)=>Dashboard("Favoritos","Seus atalhos operacionais.",null,ct);
    [HttpGet("/Educacao/GestaoExecutiva")] public Task<IActionResult> Education(CancellationToken ct)=>Dashboard("Gestão Executiva da Educação","Indicadores e pendências do ciclo educacional.","EDUCACAO",ct);
    [HttpGet("/RH/GestaoExecutiva")] public Task<IActionResult> Rh(CancellationToken ct)=>Dashboard("Gestão Executiva de RH","Indicadores de pessoas, vínculos e rotinas.","RH",ct);
    [HttpGet("/RH/FolhaGestaoExecutiva")] public Task<IActionResult> Payroll(CancellationToken ct)=>Dashboard("Gestão Executiva da Folha","Conferência, críticas e integrações da folha.","FOLHA",ct);
    [HttpGet("/Educacao/AssistenteMatricula")] public Task<IActionResult> Enrollment(CancellationToken ct)=>Assistant("MATRICULA","Assistente de Matrícula",ct);
    [HttpGet("/Educacao/AssistenteChamada")] public Task<IActionResult> Attendance(CancellationToken ct)=>Assistant("CHAMADA","Assistente de Chamada",ct);
    [HttpGet("/Educacao/AssistenteDiario")] public Task<IActionResult> Journal(CancellationToken ct)=>Assistant("DIARIO","Assistente de Fechamento de Diário",ct);
    [HttpGet("/RH/AssistenteAdmissao")] public Task<IActionResult> Hiring(CancellationToken ct)=>Assistant("ADMISSAO","Assistente de Admissão",ct);
    [HttpGet("/RH/AssistentePonto")] public Task<IActionResult> Time(CancellationToken ct)=>Assistant("PONTO","Assistente de Apuração de Ponto",ct);
    [HttpGet("/RH/AssistenteFechamentoFolha")] public Task<IActionResult> Closing(CancellationToken ct)=>Assistant("FECHAMENTO_FOLHA","Assistente de Fechamento da Folha",ct);
    [HttpPost("/AssistentesOperacionais/SalvarEtapa")][ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveAssistant([FromBody] AssistantCommand command,CancellationToken ct)
    {
        if((tenant.TenantId??0)<=0||(user.UsuarioId??0)<=0)return Forbid();
        try{using var _=System.Text.Json.JsonDocument.Parse(command.Payload);}catch(System.Text.Json.JsonException){return BadRequest(new{error="Dados da etapa inválidos."});}
        return Json(await repository.SaveAssistantAsync(tenant.TenantId!.Value,user.UsuarioId!.Value,command,HttpContext.TraceIdentifier,ct));
    }

    private async Task<IActionResult> Dashboard(string title,string description,string? module,CancellationToken ct)
    {
        if((tenant.TenantId??0)<=0)return Forbid();
        var summary=await governance.SummaryAsync(tenant.TenantId!.Value,user.UsuarioId,ct);
        if(module is not null) summary=summary with{Indicators=summary.Indicators.Where(x=>x.Module==module).ToList(),Pendencies=summary.Pendencies.Where(x=>x.Module==module).ToList()};
        ViewData["Title"]=title;ViewData["Description"]=description;return View("~/Views/ExecutiveOperations/Dashboard.cshtml",summary);
    }
    private async Task<IActionResult> Assistant(string key,string title,CancellationToken ct){var result=await Dashboard(title,"Rotina guiada com validação por etapa, persistência e auditoria.",null,ct);ViewData["AssistantKey"]=key;return result;}
}
