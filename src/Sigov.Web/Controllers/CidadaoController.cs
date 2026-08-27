using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sigov.Application.Cidadao;
using Sigov.Web.Models.Cidadao;

namespace Sigov.Web.Controllers;

public sealed class CidadaoController(ICidadaoRepository repository, ILogger<CidadaoController> logger) : Controller
{
    private static readonly string[] Categorias=["TRIBUTOS","PROTOCOLO","EDUCACAO","SAUDE","ASSISTENCIA","OBRAS","FISCALIZACAO","MEIO_AMBIENTE","OUVIDORIA","GERAL"];
    [AllowAnonymous,HttpGet("/Cidadao"),HttpGet("/Cidadao/Portal")]
    public async Task<IActionResult> Portal(long? tenantId,long? entidadeId,string? q,CancellationToken ct)
    { var c=ContextoPublico(tenantId,entidadeId);return View("Portal",new CidadaoPortalViewModel(await repository.ListarServicosAsync(c,q,null,ct),q)); }

    [AllowAnonymous,HttpGet("/Cidadao/Servicos")]
    public async Task<IActionResult> Servicos(long? tenantId,long? entidadeId,string? q,string? categoria,CancellationToken ct)
    {var c=ContextoPublico(tenantId,entidadeId);ViewBag.Categorias=Categorias;return View(new CidadaoPortalViewModel(await repository.ListarServicosAsync(c,q,categoria,ct),q,categoria));}

    [AllowAnonymous,HttpGet("/Cidadao/Servicos/Details/{id:long}"),HttpGet("/Cidadao/Servicos/Details")]
    public async Task<IActionResult> Details(long id,long? tenantId,long? entidadeId,CancellationToken ct)
    {var item=await repository.ObterServicoAsync(ContextoPublico(tenantId,entidadeId),id,ct);return item is null?NotFound():View(item);}

    [Authorize,HttpGet("/Cidadao/Solicitar")]
    public async Task<IActionResult> Solicitar(long? servico,CancellationToken ct)=>View(await Form(new AbrirSolicitacaoRequest{ServicoId=servico??0},ct));

    [Authorize,HttpPost("/Cidadao/Solicitar"),ValidateAntiForgeryToken]
    public async Task<IActionResult> Solicitar([Bind(Prefix="Form")] AbrirSolicitacaoRequest form,CancellationToken ct)
    {
        if(!ModelState.IsValid)return View(await Form(form,ct));
        try{var result=await repository.AbrirSolicitacaoAsync(ContextoAutenticado(),form,ct);TempData["Success"]="Solicitação protocolada com segurança.";return View("Comprovante",result);}
        catch(Exception ex) when(ex is InvalidOperationException or UnauthorizedAccessException or ArgumentException){logger.LogWarning("Solicitação Cidadão360 rejeitada. Referência {CorrelationId}: {Tipo}",HttpContext.TraceIdentifier,ex.GetType().Name);ModelState.AddModelError(string.Empty,ex.Message);return View(await Form(form,ct));}
    }

    [Authorize,HttpGet("/Cidadao/MinhasSolicitacoes")]
    public async Task<IActionResult> MinhasSolicitacoes(CancellationToken ct)=>View(await repository.MinhasSolicitacoesAsync(ContextoAutenticado(),ct));

    [AllowAnonymous,HttpGet("/Cidadao/Protocolo")]
    public IActionResult Protocolo()=>View(new ConsultaProtocoloViewModel());
    [AllowAnonymous,HttpPost("/Cidadao/Protocolo"),ValidateAntiForgeryToken]
    public async Task<IActionResult> Protocolo(ConsultaProtocoloViewModel model,long? tenantId,long? entidadeId,CancellationToken ct)
    {if(!ModelState.IsValid)return View(model);var c=ContextoPublico(tenantId,entidadeId);model.Resultado=await repository.ConsultarProtocoloAsync(c,model.Protocolo.Trim(),model.Verificador.Trim(),false,ct);if(model.Resultado is null)ModelState.AddModelError(string.Empty,"Protocolo ou código verificador inválido.");return View(model);}

    [HttpGet("/Cidadao/Agendamentos")] public IActionResult Agendamentos()=>Redirect("/AtendimentoCidadao/Agendamentos");
    [AllowAnonymous,HttpGet("/Cidadao/Ouvidoria")] public IActionResult Ouvidoria()=>Redirect("/Ouvidoria");
    [AllowAnonymous,HttpGet("/Cidadao/Ouvidoria/Create")] public IActionResult OuvidoriaCreate()=>Redirect("/Ouvidoria/Criar");
    [AllowAnonymous,HttpGet("/Cidadao/Ouvidoria/Acompanhar")] public IActionResult OuvidoriaAcompanhar()=>Redirect("/Ouvidoria");
    [HttpGet("/Cidadao/Atendimento")] public IActionResult Atendimento()=>Redirect("/AtendimentoCidadao/Demandas");
    [HttpGet("/Cidadao/Avaliacoes")] public IActionResult Avaliacoes()=>Redirect("/AtendimentoCidadao/Satisfacao");
    [AllowAnonymous,HttpGet("/Cidadao/Faq")] public IActionResult Faq()=>Redirect("/AtendimentoCidadao/BaseConhecimento");
    [Authorize(Policy="CIDADAO_DASHBOARD_VIEW"),HttpGet("/Cidadao/Admin/Dashboard")] public async Task<IActionResult> Dashboard(CancellationToken ct)=>View("Dashboard",await repository.DashboardAsync(ContextoAutenticado(),ct));
    [Authorize(Policy="CIDADAO_SERVICO_VIEW"),HttpGet("/Cidadao/Admin/Servicos"),HttpGet("/Cidadao/Admin/Servicos/Details"),HttpGet("/Cidadao/Admin/Categorias"),HttpGet("/Cidadao/Admin/PortalConfig")] public IActionResult Admin()=>Redirect("/AtendimentoCidadao/CartaServicos");
    [Authorize(Policy="CIDADAO_SERVICO_MANAGE"),HttpGet("/Cidadao/Admin/Servicos/Create"),HttpGet("/Cidadao/Admin/Servicos/Edit")] public IActionResult AdminEdit()=>Redirect("/AtendimentoCidadao/CartaServicos/Novo");
    [Authorize(Policy="CIDADAO_OUVIDORIA_VIEW"),HttpGet("/Cidadao/Admin/Ouvidoria"),HttpGet("/Cidadao/Admin/Ouvidoria/Details"),HttpGet("/Cidadao/Admin/Ouvidoria/Responder")] public IActionResult AdminOuvidoria()=>Redirect("/AtendimentoCidadao/Ouvidoria");
    private async Task<CidadaoSolicitarViewModel> Form(AbrirSolicitacaoRequest form,CancellationToken ct){var c=ContextoAutenticado();var services=await repository.ListarServicosAsync(c,null,null,ct);return new(form,services,services.FirstOrDefault(x=>x.Id==form.ServicoId));}
    private CidadaoContexto ContextoPublico(long? tenantId,long? entidadeId){tenantId=PositiveClaim("tenant_id")??tenantId;entidadeId=PositiveClaim("entidade_id")??entidadeId;if(tenantId is null or <=0||entidadeId is null or <=0)throw new InvalidOperationException("Portal público sem tenant_id e entidade_id configurados.");return Contexto(tenantId.Value,entidadeId.Value);}
    private CidadaoContexto ContextoAutenticado(){var t=PositiveClaim("tenant_id")??throw new UnauthorizedAccessException("tenant_id obrigatório.");var e=PositiveClaim("entidade_id")??throw new UnauthorizedAccessException("entidade_id obrigatório.");return Contexto(t,e);}
    private CidadaoContexto Contexto(long t,long e)=>new(t,e,PositiveClaim("pessoa_id"),PositiveClaim("sub")??PositiveClaim(System.Security.Claims.ClaimTypes.NameIdentifier),HttpContext.TraceIdentifier,HttpContext.Connection.RemoteIpAddress?.ToString());
    private long? PositiveClaim(string name)=>long.TryParse(User.FindFirst(name)?.Value,out var value)&&value>0?value:null;
}
