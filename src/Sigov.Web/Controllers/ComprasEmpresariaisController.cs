using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sigov.Application.ComprasEmpresariais;

namespace Sigov.Web.Controllers;

[Authorize,Route("ComprasEmpresariais")]
public sealed class ComprasEmpresariaisController(IFornecedorApplicationService fornecedores,IRequisicaoCompraApplicationService requisicoes,IComprasDashboardApplicationService dashboard,IRecebimentoCompraApplicationService recebimentos):Controller
{
 private ComprasContext Contexto(){if(!Guid.TryParse(User.FindFirst("enterprise_tenant_id")?.Value??User.FindFirst("tenant_id")?.Value,out var t)||!Guid.TryParse(User.FindFirst("sub")?.Value,out var u))throw new UnauthorizedAccessException("Tenant e usuário não resolvidos.");return new(t,u,HttpContext.TraceIdentifier);}
 [HttpGet(""),Authorize(Policy="compras_empresariais.dashboard.visualizar")]public async Task<IActionResult> Index(CancellationToken ct)=>View(await dashboard.ObterAsync(Contexto(),ct));
 [HttpGet("Fornecedores"),Authorize(Policy="compras_empresariais.fornecedores.visualizar")]public async Task<IActionResult> Fornecedores(string? busca,string? status,int pagina=1,CancellationToken ct=default)=>View("Fornecedores/Index",await fornecedores.ListarAsync(Contexto(),new(busca,status,pagina,20),ct));
 [HttpGet("Fornecedores/Novo"),Authorize(Policy="compras_empresariais.fornecedores.criar")]public IActionResult NovoFornecedor()=>View("Fornecedores/Novo");
 [HttpGet("Fornecedores/{id:guid}"),Authorize(Policy="compras_empresariais.fornecedores.visualizar")]public async Task<IActionResult> Fornecedor(Guid id,CancellationToken ct){var item=await fornecedores.ObterAsync(Contexto(),id,ct);return item is null?NotFound():View("Fornecedores/Detalhe",item);}
 [HttpGet("Fornecedores/{id:guid}/Editar"),Authorize(Policy="compras_empresariais.fornecedores.editar")]public async Task<IActionResult> EditarFornecedor(Guid id,CancellationToken ct){var item=await fornecedores.ObterAsync(Contexto(),id,ct);return item is null?NotFound():View("Fornecedores/Detalhe",item);}
 [HttpGet("Requisicoes"),Authorize(Policy="compras_empresariais.requisicoes.visualizar")]public async Task<IActionResult> Requisicoes([FromQuery]RequisicaoFiltro filtro,CancellationToken ct=default){ViewData["Filtro"]=filtro;return View("Requisicoes/Index",await requisicoes.ListarAsync(Contexto(),filtro,ct));}
 [HttpGet("Requisicoes/Nova"),Authorize(Policy="compras_empresariais.requisicoes.criar")]public IActionResult NovaRequisicao()=>View("Requisicoes/Nova");
 [HttpGet("Requisicoes/{id:guid}"),Authorize(Policy="compras_empresariais.requisicoes.visualizar")]public async Task<IActionResult> Requisicao(Guid id,string? returnUrl,CancellationToken ct){var item=await requisicoes.ObterAsync(Contexto(),id,ct);if(item is null)return NotFound();ViewData["ReturnUrl"]=Url.IsLocalUrl(returnUrl)?returnUrl:Url.Action(nameof(Requisicoes));return View("Requisicoes/Detalhe",item);}
 [HttpGet("Requisicoes/{id:guid}/Editar"),Authorize(Policy="compras_empresariais.requisicoes.editar")]public async Task<IActionResult> EditarRequisicao(Guid id,CancellationToken ct){var item=await requisicoes.ObterAsync(Contexto(),id,ct);if(item is null)return NotFound();if(item.Status!="RASCUNHO")return Conflict("Somente requisições em rascunho podem ser editadas.");return View("Requisicoes/Nova",item);}
 [HttpGet("Aprovacoes"),Authorize(Policy="compras_empresariais.aprovacoes.visualizar")]public IActionResult Aprovacoes()=>Workspace("Aprovações","Minhas aprovações e decisões por alçada.");
 [HttpGet("Cotacoes"),Authorize(Policy="compras_empresariais.cotacoes.visualizar")]public IActionResult Cotacoes()=>Workspace("Cotações","Rodadas, convites e respostas de fornecedores.");
 [HttpGet("Cotacoes/Nova"),Authorize(Policy="compras_empresariais.cotacoes.visualizar")]public IActionResult NovaCotacao()=>Workspace("Nova cotação","Configure itens, prazo e fornecedores convidados.");
 [HttpGet("Cotacoes/{id:guid}"),Authorize(Policy="compras_empresariais.cotacoes.visualizar")]public IActionResult Cotacao(Guid id)=>Workspace("Cotação","Workspace da cotação.");
 [HttpGet("Cotacoes/{id:guid}/Comparativo"),Authorize(Policy="compras_empresariais.cotacoes.visualizar")]public IActionResult Comparativo(Guid id)=>Workspace("Mapa comparativo","Julgamento humano por item e fornecedor.");
 [HttpGet("Pedidos"),Authorize(Policy="compras_empresariais.pedidos.visualizar")]public IActionResult Pedidos()=>Workspace("Pedidos","Operação integrada à jornada procure-to-pay.");
 [HttpGet("Pedidos/{id:guid}"),Authorize(Policy="compras_empresariais.pedidos.visualizar")]public IActionResult Pedido(Guid id)=>Workspace("Pedidos","Detalhe 360, histórico e ações autorizadas.");
 [HttpGet("Recebimentos"),Authorize(Policy="compras_empresariais.recebimentos.visualizar")]public async Task<IActionResult> Recebimentos([FromQuery]RecebimentoFiltro filtro,CancellationToken ct){ViewData["Filtro"]=filtro;return View("Recebimentos/Index",await recebimentos.ListarAsync(Contexto(),filtro,ct));}
 [HttpGet("Recebimentos/{id:guid}"),Authorize(Policy="compras_empresariais.recebimentos.visualizar")]public async Task<IActionResult> Recebimento(Guid id,CancellationToken ct){var item=await recebimentos.ObterAsync(Contexto(),id,ct);return item is null?NotFound():View("Recebimentos/Detalhe",item);}
 [HttpPost("Recebimentos/{id:guid}/ConcluirInspecao"),ValidateAntiForgeryToken,Authorize(Policy="compras_empresariais.recebimentos.inspecionar")]
 public async Task<IActionResult> ConcluirInspecao(Guid id,long version,string? justificativa,List<InspecaoItemRequest> itens,CancellationToken ct)
 {
  try{var result=await recebimentos.ConcluirInspecaoAsync(Contexto(),id,new(version,justificativa,itens),ct);TempData["Success"]=result.Repetido?"A conferência já havia sido concluída; exibindo o estado persistido.":"Conferência concluída e estoque atualizado com sucesso.";return RedirectToAction(nameof(Recebimento),new{id});}
  catch(ArgumentException ex){ModelState.AddModelError(string.Empty,ex.Message);}catch(InvalidOperationException ex){ModelState.AddModelError(string.Empty,ex.Message);}
  var item=await recebimentos.ObterAsync(Contexto(),id,ct);if(item is null)return NotFound();return View("Recebimentos/Detalhe",item);
 }
 [HttpGet("Pedidos/{pedidoId:guid}/Receber"),Authorize(Policy="compras_empresariais.recebimentos.registrar")]public async Task<IActionResult> NovoRecebimento(Guid pedidoId,CancellationToken ct){var pedido=await recebimentos.ObterPedidoAsync(Contexto(),pedidoId,ct);if(pedido is null)return NotFound();ViewData["IdempotencyKey"]=Guid.NewGuid().ToString("N");return View("Recebimentos/Novo",pedido);}
 [HttpPost("Pedidos/{pedidoId:guid}/Receber"),ValidateAntiForgeryToken,Authorize(Policy="compras_empresariais.recebimentos.registrar")]
 public async Task<IActionResult> ConfirmarRecebimento(Guid pedidoId,Guid almoxarifadoId,string documento,DateTimeOffset dataOperacao,string? observacoes,long pedidoVersion,string idempotencyKey,List<RecebimentoItemRequest> itens,CancellationToken ct)
 {
  try{var selecionados=itens.Where(i=>i.Quantidade>0).ToArray();var result=await recebimentos.CriarEConfirmarAsync(Contexto(),new(pedidoId,almoxarifadoId,documento,dataOperacao,observacoes,selecionados,pedidoVersion),idempotencyKey,ct);TempData["Success"] = result.Repetido?"Este recebimento já havia sido confirmado; exibindo o resultado persistido.":"Recebimento confirmado e relido com sucesso.";return RedirectToAction(nameof(Recebimento),new{id=result.Id});}
  catch(ArgumentException ex){ModelState.AddModelError(string.Empty,ex.Message);}catch(InvalidOperationException ex){ModelState.AddModelError(string.Empty,ex.Message);}
  var pedido=await recebimentos.ObterPedidoAsync(Contexto(),pedidoId,ct);if(pedido is null)return NotFound();ViewData["IdempotencyKey"]=idempotencyKey;return View("Recebimentos/Novo",pedido);
 }
 [HttpGet("Faturas"),Authorize(Policy="compras_empresariais.faturas.visualizar")]public IActionResult Faturas()=>Workspace("Faturas","Operação integrada à jornada procure-to-pay.");
 [HttpGet("Faturas/{id:guid}"),Authorize(Policy="compras_empresariais.faturas.visualizar")]public IActionResult Fatura(Guid id)=>Workspace("Faturas","Detalhe 360, histórico e ações autorizadas.");
 [HttpGet("Devolucoes"),Authorize(Policy="compras_empresariais.devolucoes.visualizar")]public IActionResult Devolucoes()=>Workspace("Devoluções","Operação integrada à jornada procure-to-pay.");
 [HttpGet("Avaliacoes"),Authorize(Policy="compras_empresariais.avaliacoes.gerenciar")]public IActionResult Avaliacoes()=>Workspace("Avaliações","Operação integrada à jornada procure-to-pay.");
 [HttpGet("Relatorios"),Authorize(Policy="compras_empresariais.relatorios.visualizar")]public async Task<IActionResult> Relatorios([FromQuery]RecebimentoFiltro filtro,CancellationToken ct){ViewData["Filtro"]=filtro;return View("Recebimentos/Relatorio",await recebimentos.ListarAsync(Contexto(),filtro with{Pagina=1,Tamanho=100},ct));}
 [HttpGet("Relatorios/Recebimentos.csv"),Authorize(Policy="compras_empresariais.relatorios.visualizar")]
 public async Task<IActionResult> ExportarRecebimentos([FromQuery]RecebimentoFiltro filtro,CancellationToken ct)
 {
  var data=await recebimentos.ListarAsync(Contexto(),filtro with{Pagina=1,Tamanho=100},ct);static string C(string? value){value??="";if(value.Length>0&&"=+-@\t\r".Contains(value[0]))value="'"+value;return "\""+value.Replace("\"","\"\"")+"\"";}var lines=new List<string>{"Pedido;Fornecedor;Documento;Destino;Situação;Data;Divergências"};lines.AddRange(data.Resultado.Items.Select(x=>string.Join(";",C(x.PedidoNumero),C(x.FornecedorNome),C(x.Documento),C(x.AlmoxarifadoNome),C(x.Status),C(x.CriadoEm.ToString("O")),x.Divergencias.ToString(System.Globalization.CultureInfo.InvariantCulture))));return File(System.Text.Encoding.UTF8.GetPreamble().Concat(System.Text.Encoding.UTF8.GetBytes(string.Join(Environment.NewLine,lines))).ToArray(),"text/csv; charset=utf-8",$"recebimentos-{DateTime.UtcNow:yyyyMMddHHmmss}.csv");
 }
 [HttpGet("Configuracao"),Authorize(Policy="compras_empresariais.configuracao.gerenciar")]public IActionResult Configuracao()=>Workspace("Configuração","Operação integrada à jornada procure-to-pay.");
 private IActionResult Workspace(string title,string description){ViewData["Title"]=title;ViewData["Description"]=description;return View("Workspace");}
}
