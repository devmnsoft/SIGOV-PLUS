using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sigov.Application.Common;
using Sigov.Application.ComprasEmpresariais;
using Sigov.Web.Models;
using System.Globalization;

namespace Sigov.Web.Controllers;

[Authorize,Route("ComprasEmpresariais")]
public sealed class ComprasEmpresariaisController(IFornecedorApplicationService fornecedores,IRequisicaoCompraApplicationService requisicoes,IComprasDashboardApplicationService dashboard,IRecebimentoCompraApplicationService recebimentos,IDivergenciaRecebimentoApplicationService divergencias,IDevolucaoCompraApplicationService devolucoes,IAprovacaoRequisicaoApplicationService aprovacoes,ICotacaoCompraApplicationService cotacoes,IFaturaCompraApplicationService faturas,IAuthorizationService authorization):Controller
{
    public ComprasEmpresariaisController(IFornecedorApplicationService fornecedores, IRequisicaoCompraApplicationService requisicoes, IComprasDashboardApplicationService dashboard, IRecebimentoCompraApplicationService recebimentos, IDivergenciaRecebimentoApplicationService divergencias, IDevolucaoCompraApplicationService devolucoes, IAprovacaoRequisicaoApplicationService aprovacoes, ICotacaoCompraApplicationService cotacoes, IAuthorizationService authorization)
        : this(fornecedores, requisicoes, dashboard, recebimentos, divergencias, devolucoes, aprovacoes, cotacoes, null!, authorization)
    {
    }
 private ComprasContext Contexto(){if(!Guid.TryParse(User.FindFirst("enterprise_tenant_id")?.Value??User.FindFirst("tenant_id")?.Value,out var t)||!Guid.TryParse(User.FindFirst("sub")?.Value,out var u))throw new UnauthorizedAccessException("Tenant e usuário não resolvidos.");return new(t,u,HttpContext.TraceIdentifier);}
 [HttpGet("")]
 public async Task<IActionResult> Index(CancellationToken ct)
 {
     if ((await authorization.AuthorizeAsync(User, "compras_empresariais.dashboard.visualizar")).Succeeded)
         return View(await dashboard.ObterAsync(Contexto(), ct));
     if ((await authorization.AuthorizeAsync(User, "compras_empresariais.requisicoes.visualizar")).Succeeded)
         return RedirectToAction(nameof(Requisicoes));
     if ((await authorization.AuthorizeAsync(User, "compras_empresariais.aprovacoes.visualizar")).Succeeded)
         return RedirectToAction(nameof(Aprovacoes));
     if ((await authorization.AuthorizeAsync(User, "compras_empresariais.fornecedores.visualizar")).Succeeded)
         return RedirectToAction(nameof(Fornecedores));
     if ((await authorization.AuthorizeAsync(User, "compras_empresariais.recebimentos.visualizar")).Succeeded)
         return RedirectToAction(nameof(Recebimentos));
     if ((await authorization.AuthorizeAsync(User, "compras_empresariais.divergencias.visualizar")).Succeeded)
         return RedirectToAction(nameof(Divergencias));
     if ((await authorization.AuthorizeAsync(User, "compras_empresariais.devolucoes.visualizar")).Succeeded)
         return RedirectToAction(nameof(Devolucoes));
     return Forbid();
 }
 [HttpGet("Fornecedores"),Authorize(Policy="compras_empresariais.fornecedores.visualizar")]public async Task<IActionResult> Fornecedores(string? busca,string? status,int pagina=1,CancellationToken ct=default)=>View("Fornecedores/Index",await fornecedores.ListarAsync(Contexto(),new(busca,status,pagina,20),ct));
 [HttpGet("Fornecedores/Novo"),Authorize(Policy="compras_empresariais.fornecedores.criar")]public IActionResult NovoFornecedor()=>View("Fornecedores/Novo");
 [HttpGet("Fornecedores/{id:guid}"),Authorize(Policy="compras_empresariais.fornecedores.visualizar")]public async Task<IActionResult> Fornecedor(Guid id,CancellationToken ct){var item=await fornecedores.ObterAsync(Contexto(),id,ct);return item is null?NotFound():View("Fornecedores/Detalhe",item);}
 [HttpGet("Fornecedores/{id:guid}/Editar"),Authorize(Policy="compras_empresariais.fornecedores.editar")]public async Task<IActionResult> EditarFornecedor(Guid id,CancellationToken ct){var item=await fornecedores.ObterAsync(Contexto(),id,ct);return item is null?NotFound():View("Fornecedores/Detalhe",item);}
 [HttpGet("Requisicoes"),Authorize(Policy="compras_empresariais.requisicoes.visualizar")]public async Task<IActionResult> Requisicoes([FromQuery]RequisicaoFiltro filtro,CancellationToken ct=default){ViewData["Filtro"]=filtro;return View("Requisicoes/Index",await requisicoes.ListarAsync(Contexto(),filtro,ct));}
 [HttpGet("Requisicoes/Nova"),Authorize(Policy="compras_empresariais.requisicoes.criar")]public IActionResult NovaRequisicao()=>View("Requisicoes/Nova");
 [HttpGet("Requisicoes/{id:guid}"),Authorize(Policy="compras_empresariais.requisicoes.visualizar")]public async Task<IActionResult> Requisicao(Guid id,string? returnUrl,CancellationToken ct){var item=await requisicoes.ObterAsync(Contexto(),id,ct);if(item is null)return NotFound();ViewData["ReturnUrl"]=Url.IsLocalUrl(returnUrl)?returnUrl:Url.Action(nameof(Requisicoes));return View("Requisicoes/Detalhe",item);}
 [HttpGet("Requisicoes/{id:guid}/Editar"),Authorize(Policy="compras_empresariais.requisicoes.editar")]public async Task<IActionResult> EditarRequisicao(Guid id,CancellationToken ct){var item=await requisicoes.ObterAsync(Contexto(),id,ct);if(item is null)return NotFound();if(item.Status is not ("RASCUNHO" or "DEVOLVIDA"))return Conflict("Somente requisições em rascunho ou devolvidas podem ser editadas.");return View("Requisicoes/Nova",item);}
 [HttpGet("Aprovacoes"),Authorize(Policy="compras_empresariais.aprovacoes.visualizar")]public async Task<IActionResult> Aprovacoes(string? busca,string? urgencia,int pagina=1,int tamanho=20,CancellationToken ct=default){var fila=await aprovacoes.ListarFilaAsync(Contexto(),pagina,tamanho,busca,urgencia,ct);var devolvidas=await aprovacoes.ListarDevolvidasAsync(Contexto(),1,10,ct);var concluidas=await aprovacoes.ListarConcluidasAsync(Contexto(),1,10,ct);ViewData["Busca"]=busca;ViewData["Urgencia"]=urgencia;return View("Aprovacoes/Index",new AprovacoesCentralViewModel(fila,devolvidas,concluidas));}
  [HttpGet("Aprovacoes/{etapaId:guid}/Detalhe"),Authorize(Policy="compras_empresariais.aprovacoes.visualizar")]public async Task<IActionResult> DetalheAprovacao(Guid etapaId,CancellationToken ct){var item=await aprovacoes.ObterDetalheAsync(Contexto(),etapaId,ct);return item is null?NotFound():View("Aprovacoes/Detalhe",item);}
 [HttpPost("Aprovacoes/{etapaId:guid}/Decidir"),Authorize(Policy="compras_empresariais.aprovacoes.aprovar")]public async Task<IActionResult> DecidirAprovacao(Guid etapaId,[FromForm(Name="decisao")]string decisao,[FromForm(Name="motivo")]string? motivo,[FromForm(Name="version")]long version,[FromForm(Name="Idempotency-Key")]string? chave,CancellationToken ct){var chaveFinal=string.IsNullOrWhiteSpace(chave)?Guid.NewGuid().ToString("N"):chave;try{var x=await aprovacoes.DecidirAsync(Contexto(),etapaId,new(decisao,motivo,version,chaveFinal),ct);TempData["Success"]=x.Repetido?"Decisão já registrada; o resultado persistido foi reapresentado.":"Decisão registrada com sucesso.";}catch(ComprasConcurrencyException ex){TempData["Conflict"]=ex.Message+" Revise o estado atual antes de criar uma nova intenção.";}catch(Exception ex)when(ex is ArgumentException or InvalidOperationException or KeyNotFoundException){TempData["Error"]=ex.Message;}return RedirectToAction(nameof(Aprovacoes));}
 [HttpPost("Aprovacoes/{requisicaoId:guid}/Reavaliar"),ValidateAntiForgeryToken,Authorize(Policy="compras_empresariais.aprovacoes.aprovar")]
 public async Task<IActionResult> ReavaliarEncaminhamento(Guid requisicaoId,[FromForm(Name="motivo")]string? motivo,[FromForm(Name="Idempotency-Key")]string? chave,string? returnUrl,CancellationToken ct)
 {
     var chaveFinal=string.IsNullOrWhiteSpace(chave)?Guid.NewGuid().ToString("N"):chave;
     try{var x=await aprovacoes.ReavaliarEncaminhamentoAsync(Contexto(),requisicaoId,new(motivo,chaveFinal),ct);TempData["Success"]=x.Mensagem;}
     catch(ComprasConcurrencyException ex){TempData["Conflict"]=ex.Message+" Revise o estado atual antes de criar uma nova intenção.";}
     catch(Exception ex)when(ex is ArgumentException or InvalidOperationException or KeyNotFoundException){TempData["Error"]=ex.Message;}
     if(Url.IsLocalUrl(returnUrl))return Redirect(returnUrl);
     return RedirectToAction(nameof(Aprovacoes));
 }
 [HttpGet("Cotacoes"),Authorize(Policy="compras_empresariais.cotacoes.visualizar")]public async Task<IActionResult> Cotacoes([FromQuery]CotacaoFiltro filtro,CancellationToken ct=default){ViewData["Filtro"]=filtro;return View("Cotacoes/Index",await cotacoes.ListarAsync(Contexto(),filtro,ct));}
 [HttpGet("Cotacoes/Nova"),Authorize(Policy="compras_empresariais.cotacoes.criar")]public async Task<IActionResult> NovaCotacao([FromQuery]Guid? requisicaoId,CancellationToken ct=default){ViewData["Elegiveis"]=await cotacoes.ListarElegiveisAsync(Contexto(),null,1,50,ct);if(requisicaoId.HasValue)ViewData["Elaboracao"]=await cotacoes.ObterElaboracaoAsync(Contexto(),requisicaoId.Value,ct);return View("Cotacoes/Nova");}
 [HttpGet("Cotacoes/{id:guid}"),Authorize(Policy="compras_empresariais.cotacoes.visualizar")]public async Task<IActionResult> Cotacao(Guid id,CancellationToken ct){var item=await cotacoes.ObterAsync(Contexto(),id,ct);if(item is null)return NotFound();ViewData["PodeEnviar"]=(await authorization.AuthorizeAsync(User,"compras_empresariais.cotacoes.enviar")).Succeeded;ViewData["PodeJulgar"]=(await authorization.AuthorizeAsync(User,"compras_empresariais.cotacoes.julgar")).Succeeded;return View("Cotacoes/Detalhe",item);}
 [HttpGet("Cotacoes/{id:guid}/Comparativo"),Authorize(Policy="compras_empresariais.cotacoes.visualizar")]public async Task<IActionResult> Comparativo(Guid id,CancellationToken ct){var item=await cotacoes.ObterComparativoAsync(Contexto(),id,ct);if(item is null)return NotFound();ViewData["PodeJulgar"]=(await authorization.AuthorizeAsync(User,"compras_empresariais.cotacoes.julgar")).Succeeded;return View("Cotacoes/Comparativo",item);}
 [HttpGet("Pedidos"),Authorize(Policy="compras_empresariais.pedidos.visualizar")]public async Task<IActionResult> Pedidos([FromQuery]PedidoFiltro filtro,CancellationToken ct=default){ViewData["Filtro"]=filtro;return View("Pedidos/Index",await cotacoes.ListarPedidosAsync(Contexto(),filtro,ct));}
 [HttpGet("Pedidos/{id:guid}"),Authorize(Policy="compras_empresariais.pedidos.visualizar")]public async Task<IActionResult> Pedido(Guid id,CancellationToken ct){var item=await cotacoes.ObterPedidoAsync(Contexto(),id,ct);if(item is null)return NotFound();ViewData["PodeReceber"]=(await authorization.AuthorizeAsync(User,"compras_empresariais.recebimentos.registrar")).Succeeded;ViewData["PodeEmitir"]=(await authorization.AuthorizeAsync(User,"compras_empresariais.pedidos.emitir")).Succeeded;ViewData["PodeCancelar"]=(await authorization.AuthorizeAsync(User,"compras_empresariais.pedidos.cancelar")).Succeeded;return View("Pedidos/Detalhe",item);}
 [HttpPost("Pedidos/{id:guid}/Encerrar"),ValidateAntiForgeryToken,Authorize(Policy="compras_empresariais.pedidos.emitir")]
 public async Task<IActionResult> EncerrarPedido(Guid id,[FromForm]long version,[FromForm]string motivo,[FromForm]bool cancelarSaldoRemanescente = true,[FromForm]string? returnUrl = null,CancellationToken ct = default)
 {
     var chave = Request.Headers["Idempotency-Key"].ToString();
     if (string.IsNullOrWhiteSpace(chave)) chave = Guid.NewGuid().ToString("N");
     try
     {
         var res = await cotacoes.EncerrarPedidoAsync(Contexto(), id, new(version, motivo, cancelarSaldoRemanescente), chave, ct);
         TempData["Success"] = res.Repetido ? "Pedido já se encontrava encerrado." : "Pedido encerrado operacionalmente com sucesso.";
     }
     catch (ComprasConcurrencyException ex) { TempData["Conflict"] = ex.Message; }
     catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or KeyNotFoundException) { TempData["Error"] = ex.Message; }
     if (Url.IsLocalUrl(returnUrl)) return Redirect(returnUrl);
     return RedirectToAction(nameof(Pedido), new { id });
 }
 [HttpPost("Pedidos/{id:guid}/Cancelar"),ValidateAntiForgeryToken,Authorize(Policy="compras_empresariais.pedidos.cancelar")]
 public async Task<IActionResult> CancelarPedido(Guid id,[FromForm]long version,[FromForm]string motivo,[FromForm]string? returnUrl,CancellationToken ct)
 {
     var chave = Request.Headers["Idempotency-Key"].ToString();
     if (string.IsNullOrWhiteSpace(chave)) chave = Guid.NewGuid().ToString("N");
     try
     {
         var res = await cotacoes.CancelarPedidoAsync(Contexto(), id, new(version, motivo), chave, ct);
         TempData["Success"] = res.Repetido ? "Pedido já se encontrava cancelado." : "Pedido cancelado com sucesso.";
     }
     catch (ComprasConcurrencyException ex) { TempData["Conflict"] = ex.Message; }
     catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or KeyNotFoundException) { TempData["Error"] = ex.Message; }
     if (Url.IsLocalUrl(returnUrl)) return Redirect(returnUrl);
     return RedirectToAction(nameof(Pedido), new { id });
 }
 [HttpGet("Recebimentos"),Authorize(Policy="compras_empresariais.recebimentos.visualizar")]public async Task<IActionResult> Recebimentos([FromQuery]RecebimentoFiltro filtro,CancellationToken ct){ViewData["Filtro"]=filtro;return View("Recebimentos/Index",await recebimentos.ListarAsync(Contexto(),filtro,ct));}
 [HttpGet("Recebimentos/{id:guid}"),Authorize(Policy="compras_empresariais.recebimentos.visualizar")]public async Task<IActionResult> Recebimento(Guid id,string? returnUrl,CancellationToken ct){var item=await recebimentos.ObterAsync(Contexto(),id,ct);if(item is null)return NotFound();var model=BuildInspection(item,null,Url.IsLocalUrl(returnUrl)?returnUrl:null);if((await authorization.AuthorizeAsync(User,"compras_empresariais.divergencias.visualizar")).Succeeded)model.Divergencias=(await divergencias.ListarAsync(Contexto(),new(RecebimentoId:id,Tamanho:100),ct)).Resultado.Items;if((await authorization.AuthorizeAsync(User,"compras_empresariais.devolucoes.visualizar")).Succeeded){model.Destinacoes=await devolucoes.ObterAcompanhamentoDestinacaoAsync(Contexto().TenantId,id,ct);model.DevolucoesVinculadas=await devolucoes.ListarPorRecebimentoAsync(Contexto().TenantId,id,ct);}return View("Recebimentos/Detalhe",model);}
 [HttpPost("Recebimentos/{id:guid}/ConcluirInspecao"),ValidateAntiForgeryToken,Authorize(Policy="compras_empresariais.recebimentos.inspecionar")]
 public async Task<IActionResult> ConcluirInspecao(Guid id,InspecaoRecebimentoViewModel model,CancellationToken ct)
 {
  var current=await recebimentos.ObterAsync(Contexto(),id,ct);if(current is null)return NotFound();model=BuildInspection(current,model,model.ReturnUrl);model.Divergencias=(await divergencias.ListarAsync(Contexto(),new(RecebimentoId:id,Tamanho:100),ct)).Resultado.Items;
  var decisions=new List<InspecaoItemRequest>();
  for(var i=0;i<model.Itens.Count;i++){var x=model.Itens[i];if(!TryDecimal(x.QuantidadeAceita,out var accepted))ModelState.AddModelError($"Itens[{i}].QuantidadeAceita","Use um número não negativo com até quatro casas decimais (ex.: 1,2500).");if(!TryDecimal(x.QuantidadeRejeitada,out var rejected))ModelState.AddModelError($"Itens[{i}].QuantidadeRejeitada","Use um número não negativo com até quatro casas decimais (ex.: 0,5000).");if(ModelState.IsValid)decisions.Add(new(x.RecebimentoItemId,accepted,rejected));}
  if(!ModelState.IsValid)return View("Recebimentos/Detalhe",model);
  try{var result=await recebimentos.ConcluirInspecaoAsync(Contexto(),id,new(model.Version,model.Justificativa,decisions),ct);TempData["Success"]=result.Repetido?"A conferência já havia sido concluída; exibindo o estado persistido.":"Conferência concluída. Somente quantidades aceitas foram movimentadas; divergências seguem para tratamento.";return RedirectToAction(nameof(Recebimento),new{id});}
  catch(ComprasConcurrencyException ex){var latest=await recebimentos.ObterAsync(Contexto(),id,ct);model.Conflito=true;model.MensagemConflito=ex.Message;model.VersaoAtual=latest?.Version;ModelState.AddModelError(string.Empty,"Conflito concorrente: revise o estado atual e inicie uma nova tentativa explicitamente.");}
  catch(ArgumentException ex){ModelState.AddModelError(string.Empty,ex.Message);}catch(InvalidOperationException ex){if(ex.Message.Contains("alterado",StringComparison.OrdinalIgnoreCase)){var latest=await recebimentos.ObterAsync(Contexto(),id,ct);model.Conflito=true;model.MensagemConflito=ex.Message;model.VersaoAtual=latest?.Version;}ModelState.AddModelError(string.Empty,ex.Message);}
  return View("Recebimentos/Detalhe",model);
 }
 [HttpGet("Recebimentos/Novo"),HttpGet("Pedidos/{pedidoId:guid}/Receber"),Authorize(Policy="compras_empresariais.recebimentos.registrar")]
 public async Task<IActionResult> NovoRecebimento([FromQuery]Guid? pedidoId,Guid? id,[FromQuery]long? divergenciaId,CancellationToken ct)
 {
  var pId = pedidoId ?? id;
  if (!pId.HasValue) return RedirectToAction(nameof(Pedidos));
  var pedido = await recebimentos.ObterPedidoAsync(Contexto(), pId.Value, ct);
  if (pedido is null) return NotFound();
  if (divergenciaId.HasValue)
  {
   var div = await divergencias.ObterAsync(Contexto(), divergenciaId.Value, ct);
   ViewData["Divergencia"] = div;
  }
  ViewData["IdempotencyKey"] = Guid.NewGuid().ToString("N");
  return View("Recebimentos/Novo", pedido);
 }
 [HttpPost("Pedidos/{pedidoId:guid}/Receber"),ValidateAntiForgeryToken,Authorize(Policy="compras_empresariais.recebimentos.registrar")]
 public async Task<IActionResult> ConfirmarRecebimento(Guid pedidoId,Guid almoxarifadoId,string documento,DateTimeOffset dataOperacao,string? observacoes,long pedidoVersion,string idempotencyKey,List<RecebimentoItemRequest> itens,CancellationToken ct)
 {
  try{var selecionados=itens.Where(i=>i.Quantidade>0).ToArray();var result=await recebimentos.CriarEConfirmarAsync(Contexto(),new(pedidoId,almoxarifadoId,documento,dataOperacao,observacoes,selecionados,pedidoVersion),idempotencyKey,ct);TempData["Success"] = result.Repetido?"Este recebimento já havia sido confirmado; exibindo o resultado persistido.":"Recebimento confirmado e relido com sucesso.";return RedirectToAction(nameof(Recebimento),new{id=result.Id});}
  catch(ArgumentException ex){ModelState.AddModelError(string.Empty,ex.Message);}catch(InvalidOperationException ex){ModelState.AddModelError(string.Empty,ex.Message);}
  var pedido=await recebimentos.ObterPedidoAsync(Contexto(),pedidoId,ct);if(pedido is null)return NotFound();ViewData["IdempotencyKey"]=idempotencyKey;return View("Recebimentos/Novo",pedido);
 }
 [HttpGet("Divergencias"),Authorize(Policy="compras_empresariais.divergencias.visualizar")]
 public async Task<IActionResult> Divergencias([FromQuery]DivergenciaFiltro filtro,CancellationToken ct){ViewData["Filtro"]=filtro;return View("Divergencias/Index",await divergencias.ListarAsync(Contexto(),filtro,ct));}
 [HttpGet("Divergencias/{id:long}"),Authorize(Policy="compras_empresariais.divergencias.visualizar")]
 public async Task<IActionResult> Divergencia(long id,string? returnUrl,CancellationToken ct){var item=await divergencias.ObterAsync(Contexto(),id,ct);if(item is null)return NotFound();var people=await divergencias.PesquisarResponsaveisAsync(Contexto(),null,1,50,ct);return View("Divergencias/Detalhe",new DivergenciaDetalheViewModel{Divergencia=item,Responsaveis=people,ReturnUrl=Url.IsLocalUrl(returnUrl)?returnUrl:Url.Action(nameof(Divergencias))});}
 [HttpPost("Divergencias/{id:long}/Atribuir"),ValidateAntiForgeryToken,Authorize(Policy="compras_empresariais.divergencias.atribuir")]
 public async Task<IActionResult> AtribuirDivergencia(long id,Guid responsavelId,long version,string idempotencyKey,string? returnUrl,CancellationToken ct)=>await DivergenceCommand(id,returnUrl,()=>divergencias.AtribuirAsync(Contexto(),id,new(responsavelId,version,idempotencyKey),ct));
 [HttpPost("Divergencias/{id:long}/Andamento"),ValidateAntiForgeryToken,Authorize(Policy="compras_empresariais.divergencias.tratar")]
 public async Task<IActionResult> AndamentoDivergencia(long id,string descricao,string? providencia,long version,string idempotencyKey,string? returnUrl,CancellationToken ct)=>await DivergenceCommand(id,returnUrl,()=>divergencias.RegistrarAndamentoAsync(Contexto(),id,new(descricao,providencia,version,idempotencyKey),ct));
 [HttpPost("Divergencias/{id:long}/Encerrar"),ValidateAntiForgeryToken,Authorize(Policy="compras_empresariais.divergencias.encerrar")]
 public async Task<IActionResult> EncerrarDivergencia(long id,string resultado,string justificativa,long version,string idempotencyKey,string? returnUrl,CancellationToken ct)=>await DivergenceCommand(id,returnUrl,()=>divergencias.EncerrarAsync(Contexto(),id,new(resultado,justificativa,version,idempotencyKey),ct));
 [HttpPost("Divergencias/{id:long}/AutorizarReposicao"),ValidateAntiForgeryToken,Authorize(Policy="compras_empresariais.divergencias.tratar")]
 public async Task<IActionResult> AutorizarReposicao(long id,[FromForm]decimal quantidade,[FromForm]string justificativa,[FromForm]long version,[FromForm]string idempotencyKey,[FromForm]string? returnUrl,CancellationToken ct)=>await DivergenceCommand(id,returnUrl,()=>divergencias.AutorizarReposicaoAsync(Contexto(),id,new(quantidade,justificativa,version,idempotencyKey),ct));
 [HttpGet("Relatorios/Divergencias.csv"),Authorize(Policy="compras_empresariais.relatorios.visualizar")]
 public async Task<IActionResult> ExportarDivergencias([FromQuery]DivergenciaFiltro filtro,CancellationToken ct){var rows=new List<DivergenciaResumo>();for(var page=1;;page++){var data=await divergencias.ListarAsync(Contexto(),filtro with{Pagina=page,Tamanho=100},ct);rows.AddRange(data.Resultado.Items);if(rows.Count>50000)throw new InvalidOperationException("A exportação excede o limite explícito de 50.000 registros; refine os filtros.");if(!data.Resultado.HasNextPage)break;}var lines=new List<string>{"Documento;Pedido;Fornecedor;Produto;Unidade;Quantidade;Motivo;Situação;Responsável;Abertura;Encerramento;Providência;Resultado"};lines.AddRange(rows.Select(x=>string.Join(';',Csv(x.Documento),Csv(x.PedidoNumero),Csv(x.Fornecedor),Csv(x.Produto),Csv(x.Unidade),x.QuantidadeRejeitada.ToString("0.####",CultureInfo.InvariantCulture),Csv(x.Motivo),Csv(x.Situacao),Csv(x.ResponsavelNome),Csv(x.AbertaEm.ToString("O")),Csv(x.EncerradaEm?.ToString("O")),Csv(x.Providencia),Csv(x.Resultado))));return CsvFile(lines,"divergencias-recebimento");}
 [HttpGet("Devolucoes"),Authorize(Policy="compras_empresariais.devolucoes.visualizar")]
 public async Task<IActionResult> Devolucoes([FromQuery]DevolucaoFiltro filtro,CancellationToken ct)
 {
  ViewData["Filtro"]=filtro;
  var tenant=Contexto().TenantId;
  ViewData["Responsaveis"]=await devolucoes.PesquisarResponsaveisAsync(tenant,null,1,100,ct);
  ViewData["OrigensElegiveis"]=await devolucoes.PesquisarOrigensElegiveisAsync(tenant,null,ct);
  return View("Devolucoes/Index",await devolucoes.ListarAsync(tenant,filtro,ct));
 }
 [HttpGet("Devolucoes/Nova"),Authorize(Policy="compras_empresariais.devolucoes.criar")]
 public async Task<IActionResult> NovaDevolucao(Guid recebimentoId,string? returnUrl,CancellationToken ct)
 {
  var tenant=Contexto().TenantId;
  var origem=await devolucoes.ObterOrigemAsync(tenant,recebimentoId,ct);
  if(origem is null)return NotFound("Recebimento não encontrado ou não elegível para preparação de devolução.");
  var snapshot=await devolucoes.ObterContextoInstitucionalAsync(tenant,Contexto().UsuarioId,ct);
  var responsaveis=await devolucoes.PesquisarResponsaveisAsync(tenant,null,1,100,ct);
  var model=new DevolucaoFormViewModel
  {
   RecebimentoId=origem.RecebimentoId,
   DocumentoRecebimento=origem.Documento,
   Fornecedor=origem.Fornecedor,
   OrigemFisica=origem.AlmoxarifadoNome??string.Empty,
   Destino=origem.Fornecedor,
   ContextoInstitucional=snapshot,
   Responsaveis=responsaveis,
   ReturnUrl=Url.IsLocalUrl(returnUrl)?returnUrl:null,
   Itens=origem.Itens.Select(x=>new DevolucaoItemLinhaViewModel
   {
    RecebimentoItemId=x.RecebimentoItemId,
    Produto=x.Produto,
    Unidade=x.Unidade,
    QuantidadeRejeitada=x.QuantidadeRejeitada,
    QuantidadeReservada=x.QuantidadeReservada,
    QuantidadeExpedida=x.QuantidadeExpedida,
    QuantidadeEntregue=x.QuantidadeEntregue,
    SaldoElegivel=x.SaldoElegivel,
    Selecionado=false,
    QuantidadeDevolver=x.SaldoElegivel>0?x.SaldoElegivel.ToString("0.####",CultureInfo.GetCultureInfo("pt-BR")):"0"
   }).ToList()
  };
  return View("Devolucoes/Nova",model);
 }
 [HttpPost("Devolucoes/Nova"),ValidateAntiForgeryToken,Authorize(Policy="compras_empresariais.devolucoes.criar")]
 public async Task<IActionResult> CriarDevolucao(DevolucaoFormViewModel model,CancellationToken ct)
 {
  var tenant=Contexto().TenantId;
  var snapshot=await devolucoes.ObterContextoInstitucionalAsync(tenant,Contexto().UsuarioId,ct);
  var responsaveis=await devolucoes.PesquisarResponsaveisAsync(tenant,null,1,100,ct);
  if(model.ResponsavelId!=Guid.Empty&&responsaveis.All(r=>r.UsuarioId!=model.ResponsavelId))
  {
   responsaveis=responsaveis.Append(new ResponsavelDivergencia(model.ResponsavelId,"Responsável selecionado","Vínculo ativo",null)).ToList();
  }
  model.ContextoInstitucional=snapshot;
  model.Responsaveis=responsaveis;

  var itensSelecionados=new List<DevolucaoItemInput>();
  var seen=new HashSet<long>();
  for(var i=0;i<model.Itens.Count;i++)
  {
   var item=model.Itens[i];
   if(!seen.Add(item.RecebimentoItemId))ModelState.AddModelError($"Itens[{i}].RecebimentoItemId","Item duplicado na seleção.");
   if(item.Selecionado)
   {
    if(!TryDecimal(item.QuantidadeDevolver,out var q))ModelState.AddModelError($"Itens[{i}].QuantidadeDevolver","Informe um número não negativo com até quatro casas decimais (ex.: 1,2500).");
    else if(q<=0)ModelState.AddModelError($"Itens[{i}].QuantidadeDevolver","A quantidade do item selecionado deve ser positiva.");
    else if(q>item.SaldoElegivel)ModelState.AddModelError($"Itens[{i}].QuantidadeDevolver",$"A quantidade ultrapassa o saldo elegível ({item.SaldoElegivel.ToString("0.####",CultureInfo.GetCultureInfo("pt-BR"))}).");
    else itensSelecionados.Add(new(item.RecebimentoItemId,q));
   }
   else
   {
    if(!string.IsNullOrWhiteSpace(item.QuantidadeDevolver)&&decimal.TryParse(item.QuantidadeDevolver,NumberStyles.Any,CultureInfo.GetCultureInfo("pt-BR"),out var qNeg)&&qNeg<0)
     ModelState.AddModelError($"Itens[{i}].QuantidadeDevolver","Quantidade negativa é inválida.");
   }
  }
  if(itensSelecionados.Count==0&&ModelState.IsValid)
   ModelState.AddModelError(string.Empty,"Pelo menos um item deve ser selecionado para compor a devolução.");
  if(model.ResponsavelId==Guid.Empty)
   ModelState.AddModelError(nameof(model.ResponsavelId),"Selecione um responsável elegível da lista.");

  if(!ModelState.IsValid)return View("Devolucoes/Nova",model);

  if(snapshot is null){ModelState.AddModelError(string.Empty,"Configuração institucional obrigatória ausente para o contexto autorizado.");return View("Devolucoes/Nova",model);}
  var request=new CriarDevolucaoRequest(
   RecebimentoId:model.RecebimentoId,
   Motivo:model.Motivo.Trim(),
   ResponsavelId:model.ResponsavelId,
   OrigemFisica:model.OrigemFisica.Trim(),
   Destino:model.Destino.Trim(),
   EsferaGoverno:snapshot.EsferaGoverno,
   TipoEntidade:snapshot.TipoEntidade,
   OrgaoSuperior:snapshot.OrgaoSuperior,
   UnidadeGestora:snapshot.UnidadeGestora,
   UnidadeExecutora:snapshot.UnidadeExecutora,
   HierarquiaAdministrativa:snapshot.HierarquiaAdministrativa,
   AbrangenciaTerritorial:snapshot.AbrangenciaTerritorial,
   Uf:snapshot.Uf,
   Municipio:snapshot.Municipio,
   Regiao:snapshot.Regiao,
   Jurisdicao:snapshot.Jurisdicao,
   Itens:itensSelecionados,
   IdempotencyKey:model.IdempotencyKey
  );

  try
  {
   var x=await devolucoes.CriarAsync(Contexto(),request,ct);
   TempData["Success"]=x.Repetido?"Rascunho já registrado; resultado persistido reapresentado.":"Rascunho criado e quantidades reservadas com sucesso.";
   return RedirectToAction(nameof(Devolucao),new{id=x.Id});
  }
  catch(ComprasConcurrencyException ex){ModelState.AddModelError(string.Empty,"Conflito concorrente: "+ex.Message);}
  catch(Exception ex)when(ex is ArgumentException or InvalidOperationException or KeyNotFoundException){ModelState.AddModelError(string.Empty,ex.Message);}
  return View("Devolucoes/Nova",model);
 }
 [HttpGet("Devolucoes/{id:long}"),Authorize(Policy="compras_empresariais.devolucoes.visualizar")]public async Task<IActionResult> Devolucao(long id,CancellationToken ct){var x=await devolucoes.ObterAsync(Contexto().TenantId,id,ct);return x is null?NotFound():View("Devolucoes/Detalhe",x);}
 [HttpGet("Devolucoes/{id:long}/Editar"),Authorize(Policy="compras_empresariais.devolucoes.editar")]
 public async Task<IActionResult> EditarDevolucao(long id,string? returnUrl,CancellationToken ct)
 {
  var tenant=Contexto().TenantId;
  var detalhe=await devolucoes.ObterParaEdicaoAsync(tenant,id,ct);
  if(detalhe is null)return NotFound("Devolução não encontrada.");
  if(detalhe.Situacao!="RASCUNHO"){TempData["Error"]="Somente devoluções na situação RASCUNHO podem ser editadas.";return RedirectToAction(nameof(Devolucao),new{id});}
  var snapshot=await devolucoes.ObterContextoInstitucionalAsync(tenant,Contexto().UsuarioId,ct);
  var responsaveis=await devolucoes.PesquisarResponsaveisAsync(tenant,null,1,100,ct);
  if(detalhe.ResponsavelId!=Guid.Empty&&responsaveis.All(r=>r.UsuarioId!=detalhe.ResponsavelId))
   responsaveis=responsaveis.Append(new ResponsavelDivergencia(detalhe.ResponsavelId,detalhe.ResponsavelNome??detalhe.ResponsavelId.ToString(),"Responsável atribuído",null)).ToList();
  var model=new DevolucaoFormViewModel
  {
   DevolucaoId=detalhe.Id,
   Version=detalhe.Version,
   RecebimentoId=detalhe.RecebimentoId,
   DocumentoRecebimento=detalhe.DocumentoRecebimento,
   Fornecedor=detalhe.Fornecedor,
   Motivo=detalhe.Motivo,
   ResponsavelId=detalhe.ResponsavelId,
   OrigemFisica=detalhe.OrigemFisica,
   Destino=detalhe.Destino,
   ContextoInstitucional=snapshot,
   Responsaveis=responsaveis,
   ReturnUrl=Url.IsLocalUrl(returnUrl)?returnUrl:null,
   Itens=detalhe.Itens.Select(x=>new DevolucaoItemLinhaViewModel
   {
    RecebimentoItemId=x.RecebimentoItemId,
    Produto=x.Produto,
    Unidade=x.Unidade,
    QuantidadeRejeitada=x.QuantidadeRejeitada,
    QuantidadeReservada=x.QuantidadeReservada,
    QuantidadeExpedida=x.QuantidadeExpedida,
    QuantidadeEntregue=x.QuantidadeEntregue,
    SaldoElegivel=x.SaldoElegivel,
    Selecionado=x.QuantidadeReservada>0,
    QuantidadeDevolver=x.QuantidadeReservada>0?x.QuantidadeReservada.ToString("0.####",CultureInfo.GetCultureInfo("pt-BR")):(x.SaldoElegivel>0?x.SaldoElegivel.ToString("0.####",CultureInfo.GetCultureInfo("pt-BR")):"0")
   }).ToList()
  };
  return View("Devolucoes/Editar",model);
 }
 [HttpPost("Devolucoes/{id:long}/Editar"),ValidateAntiForgeryToken,Authorize(Policy="compras_empresariais.devolucoes.editar")]
 public async Task<IActionResult> SalvarEdicaoDevolucao(long id,DevolucaoFormViewModel model,CancellationToken ct)
 {
  var tenant=Contexto().TenantId;
  var snapshot=await devolucoes.ObterContextoInstitucionalAsync(tenant,Contexto().UsuarioId,ct);
  var responsaveis=await devolucoes.PesquisarResponsaveisAsync(tenant,null,1,100,ct);
  if(model.ResponsavelId!=Guid.Empty&&responsaveis.All(r=>r.UsuarioId!=model.ResponsavelId))
   responsaveis=responsaveis.Append(new ResponsavelDivergencia(model.ResponsavelId,"Responsável selecionado","Vínculo ativo",null)).ToList();
  model.ContextoInstitucional=snapshot;
  model.Responsaveis=responsaveis;
  model.DevolucaoId=id;

  var itensSelecionados=new List<DevolucaoItemInput>();
  var seen=new HashSet<long>();
  for(var i=0;i<model.Itens.Count;i++)
  {
   var item=model.Itens[i];
   if(!seen.Add(item.RecebimentoItemId))ModelState.AddModelError($"Itens[{i}].RecebimentoItemId","Item duplicado na seleção.");
   if(item.Selecionado)
   {
    if(!TryDecimal(item.QuantidadeDevolver,out var q))ModelState.AddModelError($"Itens[{i}].QuantidadeDevolver","Informe um número não negativo com até quatro casas decimais (ex.: 1,2500).");
    else if(q<=0)ModelState.AddModelError($"Itens[{i}].QuantidadeDevolver","A quantidade do item selecionado deve ser positiva.");
    else if(q>item.SaldoElegivel)ModelState.AddModelError($"Itens[{i}].QuantidadeDevolver",$"A quantidade ultrapassa o saldo elegível ({item.SaldoElegivel.ToString("0.####",CultureInfo.GetCultureInfo("pt-BR"))}).");
    else itensSelecionados.Add(new(item.RecebimentoItemId,q));
   }
   else
   {
    if(!string.IsNullOrWhiteSpace(item.QuantidadeDevolver)&&decimal.TryParse(item.QuantidadeDevolver,NumberStyles.Any,CultureInfo.GetCultureInfo("pt-BR"),out var qNeg)&&qNeg<0)
     ModelState.AddModelError($"Itens[{i}].QuantidadeDevolver","Quantidade negativa é inválida.");
   }
  }
  if(itensSelecionados.Count==0&&ModelState.IsValid)
   ModelState.AddModelError(string.Empty,"Pelo menos um item deve ser selecionado para compor a devolução.");
  if(model.ResponsavelId==Guid.Empty)
   ModelState.AddModelError(nameof(model.ResponsavelId),"Selecione um responsável elegível da lista.");

  if(!ModelState.IsValid)return View("Devolucoes/Editar",model);

  var request=new EditarDevolucaoRequest(
   Motivo:model.Motivo.Trim(),
   ResponsavelId:model.ResponsavelId,
   OrigemFisica:model.OrigemFisica.Trim(),
   Destino:model.Destino.Trim(),
   Version:model.Version??0,
   Itens:itensSelecionados,
   IdempotencyKey:model.IdempotencyKey
  );

  try
  {
   var x=await devolucoes.EditarAsync(Contexto(),id,request,ct);
   TempData["Success"]=x.Repetido?"Rascunho já atualizado; estado persistido reapresentado.":"Rascunho de devolução alterado com sucesso e reservas recalculadas.";
   return RedirectToAction(nameof(Devolucao),new{id});
  }
  catch(ComprasConcurrencyException ex)
  {
   var latest=await devolucoes.ObterAsync(tenant,id,ct);
   model.Conflito=true;
   model.MensagemConflito=ex.Message;
   model.VersaoAtual=latest?.Version;
   ModelState.AddModelError(string.Empty,"Conflito concorrente: revise o estado atual e inicie nova tentativa.");
  }
  catch(Exception ex)when(ex is ArgumentException or InvalidOperationException or KeyNotFoundException)
  {
   ModelState.AddModelError(string.Empty,ex.Message);
  }
  return View("Devolucoes/Editar",model);
 }
 [HttpPost("Devolucoes/{id:long}/Expedir"),ValidateAntiForgeryToken,Authorize(Policy="compras_empresariais.devolucoes.expedir")]public Task<IActionResult> ExpedirDevolucao(long id,ExpedirDevolucaoRequest request,CancellationToken ct)=>DevolucaoCommand(id,()=>devolucoes.ExpedirAsync(Contexto(),id,request,ct),"Saída física confirmada; nenhum estoque disponível foi reduzido.");
 [HttpPost("Devolucoes/{id:long}/Entregar"),ValidateAntiForgeryToken,Authorize(Policy="compras_empresariais.devolucoes.entregar")]public Task<IActionResult> EntregarDevolucao(long id,EntregarDevolucaoRequest request,CancellationToken ct)=>DevolucaoCommand(id,()=>devolucoes.EntregarAsync(Contexto(),id,request,ct),"Entrega física ao fornecedor confirmada; não há efeito financeiro ou fiscal presumido.");
 [HttpPost("Devolucoes/{id:long}/Cancelar"),ValidateAntiForgeryToken,Authorize(Policy="compras_empresariais.devolucoes.editar")]public Task<IActionResult> CancelarDevolucao(long id,CancelarDevolucaoRequest request,CancellationToken ct)=>DevolucaoCommand(id,()=>devolucoes.CancelarAsync(Contexto(),id,request,ct),"Rascunho cancelado e reserva liberada.");
 [HttpGet("Relatorios/Devolucoes.csv"),Authorize(Policy="compras_empresariais.devolucoes.relatorio")]
 public async Task<IActionResult> ExportarDevolucoes([FromQuery]DevolucaoFiltro filtro,CancellationToken ct)
 {
  var lines=new List<string>{"Recebimento;Fornecedor;Produto;Unidade;QuantidadeRejeitada;Reservada;EmTransporte;Entregue;SaldoElegivel;Situacao;Responsavel;OrigemFisica;Destino;CriadaEm;ExpedidaEm;EntregueEm;ProtocoloEntrega"};
  var count=0;
  for(var page=1;;page++)
  {
   var data=await devolucoes.ListarAsync(Contexto().TenantId,filtro with{Pagina=page,Tamanho=100},ct);
   foreach(var x in data.Items)
   {
    ct.ThrowIfCancellationRequested();
    var det=await devolucoes.ObterAsync(Contexto().TenantId,x.Id,ct);
    if(det is not null&&det.Itens.Count>0)
    {
     foreach(var it in det.Itens)
     {
      lines.Add(string.Join(';',Csv(x.DocumentoRecebimento),Csv(x.Fornecedor),Csv(it.Produto),Csv(it.Unidade),it.QuantidadeRejeitada.ToString("0.####",CultureInfo.InvariantCulture),it.QuantidadeReservada.ToString("0.####",CultureInfo.InvariantCulture),it.QuantidadeExpedida.ToString("0.####",CultureInfo.InvariantCulture),it.QuantidadeEntregue.ToString("0.####",CultureInfo.InvariantCulture),it.SaldoElegivel.ToString("0.####",CultureInfo.InvariantCulture),Csv(x.Situacao),Csv(x.ResponsavelNome),Csv(x.OrigemFisica),Csv(x.Destino),Csv(x.CriadaEm.ToString("O")),Csv(x.ExpedidaEm?.ToString("O")),Csv(x.EntregueEm?.ToString("O")),Csv(det.DocumentoProtocolo)));
     }
    }
    else
    {
     lines.Add(string.Join(';',Csv(x.DocumentoRecebimento),Csv(x.Fornecedor),"-","-","0","0","0","0","0",Csv(x.Situacao),Csv(x.ResponsavelNome),Csv(x.OrigemFisica),Csv(x.Destino),Csv(x.CriadaEm.ToString("O")),Csv(x.ExpedidaEm?.ToString("O")),Csv(x.EntregueEm?.ToString("O")),"-"));
    }
   }
   count+=data.Items.Count;
   if(count>50000)throw new InvalidOperationException("A exportação excede o limite explícito de 50.000 registros; refine os filtros.");
   if(!data.HasNextPage)break;
  }
  return CsvFile(lines,"devolucoes-operacionais");
 }
 [HttpGet("Faturas"),Authorize(Policy="compras_empresariais.faturas.visualizar")]
  public async Task<IActionResult> Faturas([FromQuery]FaturaFiltro filtro,CancellationToken ct=default)
  {
      ViewData["Filtro"]=filtro;
      return View("Faturas/Index",await faturas.ListarAsync(Contexto(),filtro,ct));
  }
  [HttpGet("Faturas/Nova"),Authorize(Policy="compras_empresariais.faturas.criar")]
  public async Task<IActionResult> NovaFatura([FromQuery]Guid? pedidoId,CancellationToken ct=default)
  {
      if(pedidoId.HasValue&&pedidoId.Value!=Guid.Empty)
      {
          try{ViewData["Previa"]=await faturas.ObterConferenciaPreviaAsync(Contexto(),pedidoId.Value,ct);}
          catch(Exception ex){TempData["Error"]=ex.Message;}
      }
      ViewData["Pedidos"]=await cotacoes.ListarPedidosAsync(Contexto(),new PedidoFiltro(Tamanho:50),ct);
      return View("Faturas/Nova");
  }
  [HttpPost("Faturas"),ValidateAntiForgeryToken,Authorize(Policy="compras_empresariais.faturas.criar")]
  public async Task<IActionResult> CriarFatura(CriarFaturaRequest request,[FromForm(Name="Idempotency-Key")]string? chave,CancellationToken ct)
  {
      var chaveFinal=string.IsNullOrWhiteSpace(chave)?Guid.NewGuid().ToString("N"):chave;
      try
      {
          var res=await faturas.CriarAsync(Contexto(),request,chaveFinal,ct);
          TempData["Success"]=res.Repetido?"Fatura já registrada anteriormente; exibindo conferência.":$"Fatura cadastrada com sucesso! Situação: {res.Status}, Resultado Match: {res.ResultadoMatch}.";
          return RedirectToAction(nameof(DetalheFatura),new{id=res.Id});
      }
      catch(Exception ex)
      {
          TempData["Error"]=ex.Message;
          return RedirectToAction(nameof(NovaFatura),new{pedidoId=request.PedidoId});
      }
  }
  [HttpGet("Faturas/{id:guid}"),Authorize(Policy="compras_empresariais.faturas.visualizar")]
  public async Task<IActionResult> DetalheFatura(Guid id,CancellationToken ct)
  {
      var item=await faturas.ObterAsync(Contexto(),id,ct);
      if(item is null)return NotFound();
      ViewData["PodeDecidir"]=(await authorization.AuthorizeAsync(User,"compras_empresariais.faturas.decidir")).Succeeded;
      return View("Faturas/Detalhe",item);
  }
  [HttpPost("Faturas/{id:guid}/Decidir"),ValidateAntiForgeryToken,Authorize(Policy="compras_empresariais.faturas.decidir")]
  public async Task<IActionResult> DecidirFatura(Guid id,[FromForm]DecidirFaturaRequest request,CancellationToken ct)
  {
      var chaveFinal=string.IsNullOrWhiteSpace(request.IdempotencyKey)?Guid.NewGuid().ToString("N"):request.IdempotencyKey;
      try
      {
          var res=await faturas.DecidirAsync(Contexto(),id,request with{IdempotencyKey=chaveFinal},ct);
          TempData["Success"]=res.Repetido?"Decisão já processada anteriormente; resultado reapresentado.":$"Decisão '{request.Decisao}' processada com sucesso. Situação: {res.Status}.";
      }
      catch(ComprasConcurrencyException ex){TempData["Conflict"]=ex.Message;}
      catch(Exception ex){TempData["Error"]=ex.Message;}
      return RedirectToAction(nameof(DetalheFatura),new{id});
  }
  [HttpGet("Avaliacoes"),Authorize(Policy="compras_empresariais.avaliacoes.gerenciar")]public IActionResult Avaliacoes()=>Workspace("Avaliações","Operação integrada à jornada procure-to-pay.");
 [HttpGet("Relatorios"),Authorize(Policy="compras_empresariais.relatorios.visualizar")]public async Task<IActionResult> Relatorios([FromQuery]RecebimentoFiltro filtro,CancellationToken ct){ViewData["Filtro"]=filtro;return View("Recebimentos/Relatorio",await recebimentos.ListarAsync(Contexto(),filtro with{Pagina=1,Tamanho=100},ct));}
  [HttpGet("Relatorios/Aprovacoes"),Authorize(Policy="compras_empresariais.relatorios.visualizar")]public async Task<IActionResult> RelatorioAprovacoes(int pagina=1,int tamanho=20,CancellationToken ct=default)=>View("Relatorios/Aprovacoes",await aprovacoes.ListarRelatorioAsync(Contexto(),pagina,tamanho,ct));
 [HttpGet("Relatorios/Recebimentos.csv"),Authorize(Policy="compras_empresariais.relatorios.visualizar")]
 public async Task<IActionResult> ExportarRecebimentos([FromQuery]RecebimentoFiltro filtro,CancellationToken ct)
 {
  var rows=new List<RecebimentoResumo>();for(var page=1;;page++){var data=await recebimentos.ListarAsync(Contexto(),filtro with{Pagina=page,Tamanho=100},ct);rows.AddRange(data.Resultado.Items);if(rows.Count>50000)throw new InvalidOperationException("A exportação excede o limite explícito de 50.000 registros; refine os filtros.");if(!data.Resultado.HasNextPage)break;}var lines=new List<string>{"Pedido;Fornecedor;Documento;Destino;Situação;Data;Divergências"};lines.AddRange(rows.Select(x=>string.Join(";",Csv(x.PedidoNumero),Csv(x.FornecedorNome),Csv(x.Documento),Csv(x.AlmoxarifadoNome),Csv(x.Status),Csv(x.CriadoEm.ToString("O")),x.Divergencias.ToString(CultureInfo.InvariantCulture))));return CsvFile(lines,"recebimentos");
 }
 [HttpGet("Configuracao"),Authorize(Policy="compras_empresariais.configuracao.gerenciar")]public async Task<IActionResult> Configuracao(CancellationToken ct){ViewData["IdempotencyKey"]=Guid.NewGuid().ToString("N");return View("Configuracao/Politica",await aprovacoes.ObterPoliticaAsync(Contexto(),ct));}
 [HttpPost("Configuracao"),Authorize(Policy="compras_empresariais.configuracao.gerenciar")]public async Task<IActionResult> SalvarConfiguracao([FromForm(Name="Nome")]string? nome,[FromForm(Name="Niveis")]decimal[]? limites,[FromForm(Name="IdempotencyKey")]string? chave,CancellationToken ct){var niveis=(limites??Array.Empty<decimal>()).Where(v=>v>0m).Select(v=>new NivelPoliticaRequest(v)).ToList();var chaveFinal=string.IsNullOrWhiteSpace(chave)?Guid.NewGuid().ToString("N"):chave;try{var x=await aprovacoes.SalvarPoliticaAsync(Contexto(),new(nome,niveis),chaveFinal,ct);TempData["Success"]=x.Repetido?"Política já registrada; o resultado persistido foi reapresentado.":$"Política \"{x.Nome}\" salva com sucesso.";ViewData["IdempotencyKey"]=Guid.NewGuid().ToString("N");return View("Configuracao/Politica",await aprovacoes.ObterPoliticaAsync(Contexto(),ct));}catch(ComprasConcurrencyException ex){TempData["Conflict"]=ex.Message+" Revise o estado atual antes de criar uma nova intenção.";ViewData["Nome"]=nome;ViewData["Limites"]=limites;ViewData["IdempotencyKey"]=Guid.NewGuid().ToString("N");return View("Configuracao/Politica",await aprovacoes.ObterPoliticaAsync(Contexto(),ct));}catch(Exception ex)when(ex is ArgumentException or InvalidOperationException or KeyNotFoundException){TempData["Error"]=ex.Message;ViewData["Nome"]=nome;ViewData["Limites"]=limites;ViewData["IdempotencyKey"]=Guid.NewGuid().ToString("N");return View("Configuracao/Politica",await aprovacoes.ObterPoliticaAsync(Contexto(),ct));}}
 [HttpGet("Relatorios/Aprovacoes.csv"),Authorize(Policy="compras_empresariais.relatorios.visualizar")]public async Task<IActionResult> RelatorioAprovacoesCsv(CancellationToken ct){var rows=new List<AprovacaoRelatorioLinha>();for(var page=1;;page++){var data=await aprovacoes.ListarRelatorioAsync(Contexto(),page,100,ct);rows.AddRange(data.Items);if(rows.Count>50000)throw new InvalidOperationException("A exportação excede o limite explícito de 50.000 registros; refine os filtros.");if(!data.HasNextPage)break;}var lines=new List<string>{"Numero;Ciclo;Etapa;Alcada;SituacaoEtapa;Aprovador;StatusRequisicao;Total;CriadaEm;DecididaEm;Motivo"};lines.AddRange(rows.Select(x=>string.Join(";",Csv(x.Numero),Csv(x.Ciclo.ToString(CultureInfo.InvariantCulture)),Csv(x.Etapa.ToString(CultureInfo.InvariantCulture)),Csv(x.Alcada.ToString("0.00",CultureInfo.InvariantCulture)),Csv(x.SituacaoEtapa),Csv(x.AprovadorSub),Csv(x.StatusRequisicao),Csv(x.Total.ToString("0.00",CultureInfo.InvariantCulture)),x.CriadaEm.ToString("O"),x.DecididaEm?.ToString("O")??"",Csv(x.Motivo))));return CsvFile(lines,"aprovacoes");}
 private async Task<IActionResult> DevolucaoCommand(long id,Func<Task<DevolucaoComandoResultado>> action,string success){try{var x=await action();TempData["Success"]=x.Repetido?"Comando já processado; resultado persistido reapresentado.":success;}catch(ComprasConcurrencyException ex){TempData["Conflict"]=ex.Message+" Revise o estado atual antes de criar uma nova intenção.";}catch(Exception ex)when(ex is ArgumentException or InvalidOperationException or KeyNotFoundException){TempData["Error"]=ex.Message;}return RedirectToAction(nameof(Devolucao),new{id});}
 private async Task<IActionResult> DivergenceCommand(long id,string? returnUrl,Func<Task<DivergenciaComandoResultado>> action){try{var result=await action();TempData["Success"]=result.Repetido?"Comando já processado; resultado persistido reapresentado.":result.PendenciaConcluida?"Divergência encerrada e pendência agregada concluída.":"Alteração registrada no histórico.";return RedirectToAction(nameof(Divergencia),new{id,returnUrl});}catch(ArgumentException ex){TempData["Error"]=ex.Message;}catch(InvalidOperationException ex){TempData["Error"]=ex.Message;}return RedirectToAction(nameof(Divergencia),new{id,returnUrl});}
 private static InspecaoRecebimentoViewModel BuildInspection(RecebimentoDetalhe receipt,InspecaoRecebimentoViewModel? submitted,string? returnUrl)=>new(){Recebimento=receipt,Version=submitted?.Version??receipt.Version,Justificativa=submitted?.Justificativa,Itens=submitted?.Itens.Count>0?submitted.Itens:receipt.Itens.Where(x=>x.QuantidadeConferencia>0).Select(x=>new InspecaoDecisaoViewModel{RecebimentoItemId=x.Id,QuantidadeAceita=x.QuantidadeConferencia.ToString("0.####",CultureInfo.GetCultureInfo("pt-BR")),QuantidadeRejeitada="0"}).ToList(),Conflito=submitted?.Conflito??false,MensagemConflito=submitted?.MensagemConflito,VersaoAtual=submitted?.VersaoAtual,ReturnUrl=returnUrl};
 private static bool TryDecimal(string value,out decimal number){return decimal.TryParse(value,NumberStyles.AllowDecimalPoint|NumberStyles.AllowLeadingSign,CultureInfo.GetCultureInfo("pt-BR"),out number)&&number>=0&&decimal.Round(number,4)==number;}
 private static string Csv(string? value){value??="";if(value.Length>0&&"=+-@\t\r".Contains(value[0]))value="'"+value;return "\""+value.Replace("\"","\"\"")+"\"";}
 private IActionResult CsvFile(IEnumerable<string> lines,string prefix)=>File(System.Text.Encoding.UTF8.GetPreamble().Concat(System.Text.Encoding.UTF8.GetBytes(string.Join(Environment.NewLine,lines))).ToArray(),"text/csv; charset=utf-8",$"{prefix}-{DateTime.UtcNow:yyyyMMddHHmmss}.csv");
 private IActionResult Workspace(string title,string description){ViewData["Title"]=title;ViewData["Description"]=description;return View("Workspace");}
}
public sealed record AprovacoesCentralViewModel(PagedResult<AprovacaoFilaResumo> Fila,PagedResult<AprovacaoPainelResumo> Devolvidas,PagedResult<AprovacaoPainelResumo> Concluidas);
