using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sigov.Application.Abstractions;
using Sigov.Application.Authorization;
using Sigov.Application.Patrimonio;

namespace Sigov.Web.Controllers;

[Authorize]
public sealed class PatrimonioController(IPatrimonioService service,ICurrentTenant tenant,ICurrentUser user,IAuthorizationEvaluator authorization) : Controller
{
    [HttpGet("/Patrimonio"),HttpGet("/Patrimonio/Dashboard")]
    public async Task<IActionResult> Dashboard(CancellationToken ct){if(!await Permitido(PatrimonioPermissoes.DashboardVisualizar,ct))return Forbid();return View(await service.ObterDashboardAsync(Tenant(),ct));}
    [HttpGet("/Patrimonio/Bens")]
    public async Task<IActionResult> Bens([FromQuery]PatrimonioBemFiltro filtro,CancellationToken ct){if(!await Permitido(PatrimonioPermissoes.BemVisualizar,ct))return Forbid();ViewBag.PodeCriar=await Permitido(PatrimonioPermissoes.BemCriar,ct);ViewBag.PodeMovimentar=await Permitido(PatrimonioPermissoes.BemMovimentar,ct);ViewBag.PodeBaixar=await Permitido(PatrimonioPermissoes.BemBaixar,ct);ViewBag.PodeExportar=await Permitido(PatrimonioPermissoes.Exportar,ct);return View(await service.ListarBensAsync(Tenant(),filtro,ct));}
    [HttpGet("/Patrimonio/Bens/Novo"),HttpGet("/Patrimonio/Bens/Create")]
    public async Task<IActionResult> Novo(CancellationToken ct)=>await Permitido(PatrimonioPermissoes.BemCriar,ct)?View("Formulario",null):Forbid();
    [HttpPost("/Patrimonio/Bens/Novo"),ValidateAntiForgeryToken]
    public async Task<IActionResult> Novo(PatrimonioBemInput input,CancellationToken ct){if(!await Permitido(PatrimonioPermissoes.BemCriar,ct))return Forbid();try{var id=await service.CriarBemAsync(Tenant(),Usuario(),HttpContext.TraceIdentifier,input,ct);TempData["Success"]="Bem tombado com sucesso.";return Redirect($"/Patrimonio/Bens/{id}");}catch(Exception e) when(e is ArgumentException or InvalidOperationException){ModelState.AddModelError("",e.Message);return View("Formulario",input);}}
    [HttpGet("/Patrimonio/Bens/{id:long}"),HttpGet("/Patrimonio/Bens/Details/{id:long}")]
    public async Task<IActionResult> Detalhe(long id,CancellationToken ct){if(!await Permitido(PatrimonioPermissoes.BemVisualizar,ct))return Forbid();var bem=await service.ObterBemAsync(Tenant(),id,ct);return bem is null?NotFound():View(bem);}
    [HttpGet("/Patrimonio/Bens/{id:long}/TermoResponsabilidade")]
    public async Task<IActionResult> TermoResponsabilidade(long id,CancellationToken ct){if(!await Permitido(PatrimonioPermissoes.BemVisualizar,ct))return Forbid();var bem=await service.ObterBemAsync(Tenant(),id,ct);return bem is null?NotFound():View("TermoResponsabilidade",bem);}
    [HttpGet("/Patrimonio/Bens/{id:long}/Editar"),HttpGet("/Patrimonio/Bens/Edit/{id:long}")]
    public async Task<IActionResult> Editar(long id,CancellationToken ct){if(!await Permitido(PatrimonioPermissoes.BemEditar,ct))return Forbid();var b=await service.ObterBemAsync(Tenant(),id,ct);if(b is null)return NotFound();ViewBag.Id=id;return View("Formulario",new PatrimonioBemInput(b.CodigoTombo,b.Descricao,b.CategoriaId,b.TipoBem,null,b.Marca,b.Modelo,b.NumeroSerie,b.DataAquisicao,b.ValorAquisicao,b.ValorAtual,b.EstadoConservacao,b.UnidadeId,b.SetorId,b.ResponsavelUsuarioId,b.Localizacao,b.Observacao));}
    [HttpPost("/Patrimonio/Bens/{id:long}/Editar"),ValidateAntiForgeryToken]
    public async Task<IActionResult> Editar(long id,PatrimonioBemInput input,CancellationToken ct){if(!await Permitido(PatrimonioPermissoes.BemEditar,ct))return Forbid();try{await service.EditarBemAsync(Tenant(),Usuario(),HttpContext.TraceIdentifier,id,input,ct);TempData["Success"]="Bem atualizado.";return Redirect($"/Patrimonio/Bens/{id}");}catch(Exception e)when(e is ArgumentException or InvalidOperationException){ModelState.AddModelError("",e.Message);ViewBag.Id=id;return View("Formulario",input);}}
    [HttpPost("/Patrimonio/Bens/{id:long}/Movimentar"),ValidateAntiForgeryToken]
    public async Task<IActionResult> Movimentar(long id,PatrimonioMovimentacaoInput input,CancellationToken ct){if(!await Permitido(PatrimonioPermissoes.BemMovimentar,ct))return Forbid();await service.MovimentarBemAsync(Tenant(),Usuario(),HttpContext.TraceIdentifier,id,input,ct);TempData["Success"]="Movimentação registrada e auditada.";return Redirect($"/Patrimonio/Bens/{id}");}
    [HttpPost("/Patrimonio/Bens/{id:long}/Baixar"),ValidateAntiForgeryToken]
    public async Task<IActionResult> Baixar(long id,PatrimonioBaixaInput input,CancellationToken ct){if(!await Permitido(PatrimonioPermissoes.BemBaixar,ct))return Forbid();await service.BaixarBemAsync(Tenant(),Usuario(),HttpContext.TraceIdentifier,id,input,ct);TempData["Success"]="Baixa patrimonial registrada.";return Redirect($"/Patrimonio/Bens/{id}");}
    [HttpGet("/Patrimonio/Inventarios")]
    public async Task<IActionResult> Inventarios(int pagina=1,CancellationToken ct=default){if(!await Permitido(PatrimonioPermissoes.InventarioVisualizar,ct))return Forbid();ViewBag.PodeCriar=await Permitido(PatrimonioPermissoes.InventarioCriar,ct);return View(await service.ListarInventariosAsync(Tenant(),pagina,25,ct));}
    [HttpPost("/Patrimonio/Inventarios"),ValidateAntiForgeryToken]
    public async Task<IActionResult> AbrirInventario(PatrimonioInventarioInput input,CancellationToken ct){if(!await Permitido(PatrimonioPermissoes.InventarioCriar,ct))return Forbid();var id=await service.AbrirInventarioAsync(Tenant(),Usuario(),HttpContext.TraceIdentifier,input,ct);return Redirect($"/Patrimonio/Inventarios/{id}");}
    [HttpGet("/Patrimonio/Inventarios/{id:long}")]
    public async Task<IActionResult> Inventario(long id,CancellationToken ct){if(!await Permitido(PatrimonioPermissoes.InventarioVisualizar,ct))return Forbid();ViewBag.PodeConferir=await Permitido(PatrimonioPermissoes.InventarioConferir,ct);ViewBag.PodeFechar=await Permitido(PatrimonioPermissoes.InventarioCriar,ct);var x=await service.ObterInventarioAsync(Tenant(),id,ct);return x is null?NotFound():View(x);}
    [HttpPost("/Patrimonio/Inventarios/{id:long}/Itens/{itemId:long}/Conferir"),ValidateAntiForgeryToken]
    public async Task<IActionResult> Conferir(long id,long itemId,PatrimonioConferenciaInput input,CancellationToken ct){if(!await Permitido(PatrimonioPermissoes.InventarioConferir,ct))return Forbid();await service.ConferirItemAsync(Tenant(),Usuario(),HttpContext.TraceIdentifier,id,itemId,input,ct);return Redirect($"/Patrimonio/Inventarios/{id}");}
    [HttpPost("/Patrimonio/Inventarios/{id:long}/Fechar"),ValidateAntiForgeryToken]
    public async Task<IActionResult> Fechar(long id,CancellationToken ct){if(!await Permitido(PatrimonioPermissoes.InventarioCriar,ct))return Forbid();await service.FecharInventarioAsync(Tenant(),Usuario(),HttpContext.TraceIdentifier,id,ct);return Redirect($"/Patrimonio/Inventarios/{id}");}
    [HttpGet("/Patrimonio/Bens/Exportar")]
    public async Task<IActionResult> Exportar([FromQuery]PatrimonioBemFiltro filtro,CancellationToken ct){if(!await Permitido(PatrimonioPermissoes.Exportar,ct))return Forbid();return File(await service.ExportarCsvAsync(Tenant(),filtro,ct),"text/csv; charset=utf-8","patrimonio-bens.csv");}
    [HttpGet("/Patrimonio/Movimentacoes")] public IActionResult Movimentacoes()=>Redirect("/Patrimonio/Bens");
    [HttpGet("/Patrimonio/Baixas")] public IActionResult Baixas()=>Redirect("/Patrimonio/Bens?status=BAIXADO");
    [HttpGet("/Patrimonio/Depreciacao")] public IActionResult Depreciacao()=>Redirect("/Ativos/Patrimonio/Depreciacao");
    [HttpGet("/Patrimonio/Relatorios")] public IActionResult Relatorios()=>Redirect("/Ativos/Relatorios");
    [HttpGet("/Patrimonio/Imoveis"),HttpGet("/Patrimonio/Imoveis/Create"),HttpGet("/Patrimonio/Imoveis/Edit/{id:long?}"),HttpGet("/Patrimonio/Imoveis/Details/{id:long?}"),HttpGet("/Patrimonio/Imoveis/Ocupacao"),HttpGet("/Patrimonio/Imoveis/Documentos")]
    public IActionResult Imoveis()=>Redirect("/Ativos/Patrimonio");
    private async Task<bool> Permitido(string chave,CancellationToken ct){var i=chave.LastIndexOf('.');var d=await authorization.EvaluateAsync(new(Usuario(),"patrimonio",chave[..i],chave[(i+1)..],Tenant(),tenant.EntidadeId,tenant.ExercicioId,null,null,HttpContext.TraceIdentifier,"WEB_FUNC01"),ct);return d.Permitido;}
    private long Tenant()=>tenant.TenantId??throw new InvalidOperationException("tenant_id obrigatório.");private long Usuario()=>user.UsuarioId??throw new InvalidOperationException("Usuário obrigatório.");
}
