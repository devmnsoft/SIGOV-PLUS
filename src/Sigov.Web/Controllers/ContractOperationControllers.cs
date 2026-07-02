using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sigov.Web.Models.Operational;
using Sigov.Web.Services.Operational;

namespace Sigov.Web.Controllers;

[Authorize]
public abstract class ContractOperationControllerBase<TService> : Controller where TService : ContractOperationServiceBase
{
    protected readonly TService Service; private readonly ILogger _logger;
    protected ContractOperationControllerBase(TService service, ILogger logger){Service=service;_logger=logger;}
    protected async Task<IActionResult> Hub(CancellationToken ct){ try { return View("~/Views/Operational/Hub.cshtml", await Service.GetAsync(ct)); } catch(Exception ex){ _logger.LogError(ex,"Falha controlada em área contratual"); return View("~/Views/Operational/Hub.cshtml", new OperationalHubViewModel{Title="Operação contratual",Description="Falha controlada; tente novamente.",FallbackMessage="Não foi possível consultar o schema neste momento."}); } }
    protected async Task<IActionResult> Critical(string action, string redirect, CancellationToken ct){ var ok=await Service.RegistrarAcaoAsync(action,"Ação crítica confirmada por modal",ct); TempData["Toast"] = ok ? "Ação registrada/auditada quando o schema permitir." : "Ação em fallback honesto; nenhum salvamento foi simulado."; return Redirect(redirect); }
}

public sealed class ImplantacaoController : ContractOperationControllerBase<ImplantacaoService>
{ public ImplantacaoController(ImplantacaoService s, ILogger<ImplantacaoController> l):base(s,l){} [HttpGet("/Implantacao")][HttpGet("/Implantacao/Projetos")][HttpGet("/Implantacao/Projetos/Novo")][HttpGet("/Implantacao/Projetos/{id:long}")][HttpGet("/Implantacao/Projetos/{id:long}/Etapas")][HttpGet("/Implantacao/Projetos/{id:long}/Evidencias")][HttpGet("/Implantacao/Projetos/{id:long}/TermoAceite")] public Task<IActionResult> Index(CancellationToken ct)=>Hub(ct); [HttpPost("/Implantacao/Projetos/{id:long}/ConcluirEtapa")][ValidateAntiForgeryToken] public Task<IActionResult> ConcluirEtapa(long id,CancellationToken ct)=>Critical("ConcluirEtapa",$"/Implantacao/Projetos/{id}/Etapas",ct); }
public sealed class MigracaoController : ContractOperationControllerBase<MigracaoService>
{ public MigracaoController(MigracaoService s, ILogger<MigracaoController> l):base(s,l){} [HttpGet("/Migracao")][HttpGet("/Migracao/Lotes")][HttpGet("/Migracao/Lotes/Novo")][HttpGet("/Migracao/Lotes/{id:long}")][HttpGet("/Migracao/Logs")][HttpGet("/Migracao/Validacoes")][HttpGet("/Migracao/Importar")] public Task<IActionResult> Index(CancellationToken ct)=>Hub(ct); [HttpPost("/Migracao/Importar")][ValidateAntiForgeryToken] public Task<IActionResult> Importar(CancellationToken ct)=>Critical("Importar","/Migracao",ct); }
public sealed class TreinamentosController : ContractOperationControllerBase<TreinamentoService>
{ public TreinamentosController(TreinamentoService s, ILogger<TreinamentosController> l):base(s,l){} [HttpGet("/Treinamentos")][HttpGet("/Treinamentos/Turmas")][HttpGet("/Treinamentos/Turmas/Nova")][HttpGet("/Treinamentos/Turmas/{id:long}")][HttpGet("/Treinamentos/Participantes")][HttpGet("/Treinamentos/Certificados")][HttpGet("/Treinamentos/Avaliacoes")] public Task<IActionResult> Index(CancellationToken ct)=>Hub(ct); [HttpPost("/Treinamentos/Certificados/Emitir")][ValidateAntiForgeryToken] public Task<IActionResult> Emitir(CancellationToken ct)=>Critical("EmitirCertificado","/Treinamentos/Certificados",ct); }
public sealed class SuporteController : ContractOperationControllerBase<SuporteService>
{ public SuporteController(SuporteService s, ILogger<SuporteController> l):base(s,l){} [HttpGet("/Suporte")][HttpGet("/Suporte/Chamados")][HttpGet("/Suporte/Chamados/Novo")][HttpGet("/Suporte/Chamados/{id:long}")][HttpGet("/Suporte/MeusChamados")][HttpGet("/Suporte/Painel")][HttpGet("/Suporte/Satisfacao")] public Task<IActionResult> Index(CancellationToken ct)=>Hub(ct); [HttpPost("/Suporte/Chamados/Novo")][ValidateAntiForgeryToken] public Task<IActionResult> Novo(CancellationToken ct)=>Critical("AbrirChamado","/Suporte/Chamados",ct); }
public sealed class SlaController : ContractOperationControllerBase<SlaService>
{ public SlaController(SlaService s, ILogger<SlaController> l):base(s,l){} [HttpGet("/Sla")][HttpGet("/Sla/Regras")][HttpGet("/Sla/Monitoramento")][HttpGet("/Sla/Eventos")][HttpGet("/Sla/Relatorios")] public Task<IActionResult> Index(CancellationToken ct)=>Hub(ct); [HttpPost("/Sla/Regras")][ValidateAntiForgeryToken] public Task<IActionResult> Regra(CancellationToken ct)=>Critical("AlterarRegra","/Sla/Regras",ct); }
public sealed class PocController : ContractOperationControllerBase<PocService>
{ public PocController(PocService s, ILogger<PocController> l):base(s,l){} [HttpGet("/Poc")][HttpGet("/Poc/Roteiros")][HttpGet("/Poc/Roteiros/Novo")][HttpGet("/Poc/Roteiros/{id:long}")][HttpGet("/Poc/Execucoes")][HttpGet("/Poc/Execucoes/{id:long}")][HttpGet("/Poc/Requisitos")][HttpGet("/Poc/Evidencias")][HttpGet("/Poc/Relatorio")] public Task<IActionResult> Index(CancellationToken ct)=>Hub(ct); [HttpPost("/Poc/Execucoes/{id:long}")][ValidateAntiForgeryToken] public Task<IActionResult> Avaliar(long id,CancellationToken ct)=>Critical("AvaliarRequisito",$"/Poc/Execucoes/{id}",ct); }
public sealed class AceitesController : ContractOperationControllerBase<AceiteFormalService>
{ public AceitesController(AceiteFormalService s, ILogger<AceitesController> l):base(s,l){} [HttpGet("/Aceites")][HttpGet("/Aceites/Novo")][HttpGet("/Aceites/{id:long}")][HttpGet("/Aceites/Pendentes")][HttpGet("/Aceites/Concluidos")] public Task<IActionResult> Index(CancellationToken ct)=>Hub(ct); [HttpPost("/Aceites/{id:long}/Decidir")][ValidateAntiForgeryToken] public Task<IActionResult> Decidir(long id,CancellationToken ct)=>Critical("DecidirAceite",$"/Aceites/{id}",ct); }
