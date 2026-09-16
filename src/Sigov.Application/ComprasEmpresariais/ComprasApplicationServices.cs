namespace Sigov.Application.ComprasEmpresariais;

internal static class ComprasGuard
{
 public static void Context(ComprasContext c){if(c.TenantId==Guid.Empty||c.UsuarioId==Guid.Empty)throw new UnauthorizedAccessException("Tenant e usuário autenticados são obrigatórios.");}
 public static void Key(string key){if(string.IsNullOrWhiteSpace(key)||key.Length>200)throw new ArgumentException("Idempotency-Key válida é obrigatória.");}
}
public sealed class FornecedorApplicationService(IFornecedorRepository repository):IFornecedorApplicationService
{
 public Task<Common.PagedResult<FornecedorResumo>> ListarAsync(ComprasContext c,FornecedorFiltro f,CancellationToken ct){ComprasGuard.Context(c);return repository.ListarAsync(c.TenantId,f,ct);}
 public Task<FornecedorResumo?> ObterAsync(ComprasContext c,Guid id,CancellationToken ct){ComprasGuard.Context(c);return repository.ObterAsync(c.TenantId,id,ct);}
 public Task<Guid> CriarAsync(ComprasContext c,CriarFornecedorRequest r,string key,CancellationToken ct){ComprasGuard.Context(c);ComprasGuard.Key(key);if(string.IsNullOrWhiteSpace(r.Documento)||string.IsNullOrWhiteSpace(r.RazaoSocial))throw new ArgumentException("Documento e razão social são obrigatórios.");return repository.CriarAsync(c,r,key,ct);}
 public Task AlterarStatusAsync(ComprasContext c,Guid id,AlterarStatusRequest r,CancellationToken ct){ComprasGuard.Context(c);return repository.AlterarStatusAsync(c,id,r,ct);}
 public Task AdicionarContatoAsync(ComprasContext c,Guid id,AdicionarContatoRequest r,string key,CancellationToken ct){ComprasGuard.Context(c);ComprasGuard.Key(key);return repository.AdicionarContatoAsync(c,id,r,key,ct);}
 public Task AdicionarEnderecoAsync(ComprasContext c,Guid id,AdicionarEnderecoRequest r,string key,CancellationToken ct){ComprasGuard.Context(c);ComprasGuard.Key(key);return repository.AdicionarEnderecoAsync(c,id,r,key,ct);}
 public Task AdicionarDocumentoAsync(ComprasContext c,Guid id,AdicionarDocumentoRequest r,string key,CancellationToken ct){ComprasGuard.Context(c);ComprasGuard.Key(key);return repository.AdicionarDocumentoAsync(c,id,r,key,ct);}
}
public sealed class RequisicaoCompraApplicationService(IRequisicaoCompraRepository repository):IRequisicaoCompraApplicationService
{
 public Task<Common.PagedResult<RequisicaoResumo>> ListarAsync(ComprasContext c,RequisicaoFiltro f,CancellationToken ct){ComprasGuard.Context(c);if(f.DataInicial.HasValue&&f.DataFinal.HasValue&&f.DataInicial>f.DataFinal)throw new ArgumentException("O período informado é inválido.");var status=string.IsNullOrWhiteSpace(f.Status)?null:f.Status.Trim().ToUpperInvariant();if(status is not null and not ("RASCUNHO" or "PENDENTE_APROVACAO" or "APROVADA" or "REJEITADA" or "DEVOLVIDA" or "CANCELADA"))throw new ArgumentException("A situação informada é inválida.");return repository.ListarAsync(c.TenantId,f with{Status=status},ct);}
 public Task<RequisicaoDetalhe?> ObterAsync(ComprasContext c,Guid id,CancellationToken ct){ComprasGuard.Context(c);if(id==Guid.Empty)throw new ArgumentException("Requisição inválida.");return repository.ObterAsync(c.TenantId,id,ct);}
 public Task<Guid> CriarAsync(ComprasContext c,CriarRequisicaoRequest r,string key,CancellationToken ct){ComprasGuard.Context(c);ComprasGuard.Key(key);Validate(r.Justificativa,r.Urgencia,r.Itens);return repository.CriarAsync(c,r,key,ct);}
 public Task AtualizarAsync(ComprasContext c,Guid id,AtualizarRequisicaoRequest r,CancellationToken ct){ComprasGuard.Context(c);if(id==Guid.Empty||r.Version<=0)throw new ArgumentException("Requisição ou versão inválida.");Validate(r.Justificativa,r.Urgencia,r.Itens);return repository.AtualizarAsync(c,id,r,ct);}
 public Task EnviarAsync(ComprasContext c,Guid id,long version,CancellationToken ct){ComprasGuard.Context(c);if(id==Guid.Empty||version<=0)throw new ArgumentException("Requisição ou versão inválida.");return repository.EnviarAsync(c,id,version,ct);}

 private static void Validate(string justificativa,string urgencia,IReadOnlyList<RequisicaoItemRequest> itens)
 {
  if(string.IsNullOrWhiteSpace(justificativa)||justificativa.Trim().Length<10||justificativa.Length>2000)throw new ArgumentException("A justificativa deve ter entre 10 e 2.000 caracteres.");
  if(urgencia?.Trim().ToUpperInvariant() is not ("NORMAL" or "ALTA" or "CRITICA"))throw new ArgumentException("A urgência informada é inválida.");
  if(itens.Count==0)throw new ArgumentException("Inclua ao menos um item.");
  if(itens.Count>200)throw new ArgumentException("A requisição não pode conter mais de 200 itens.");
  if(itens.Any(x=>x.Tipo?.Trim().ToUpperInvariant() is not ("MATERIAL" or "SERVICO" or "ATIVO" or "PECA_OS")||string.IsNullOrWhiteSpace(x.Descricao)||x.Descricao.Length>300||string.IsNullOrWhiteSpace(x.Unidade)||x.Unidade.Length>20||x.Quantidade<=0||x.ValorEstimado<0))throw new ArgumentException("Revise tipo, descrição, unidade, quantidade e valor dos itens.");
  if(itens.GroupBy(x=>$"{x.Tipo.Trim().ToUpperInvariant()}|{x.Descricao.Trim().ToUpperInvariant()}|{x.Unidade.Trim().ToUpperInvariant()}").Any(x=>x.Count()>1))throw new ArgumentException("Consolide os itens repetidos antes de salvar.");
 }
}
public sealed class ComprasDashboardApplicationService(IComprasDashboardRepository repository):IComprasDashboardApplicationService
{ public Task<ComprasDashboard> ObterAsync(ComprasContext c,CancellationToken ct){ComprasGuard.Context(c);return repository.ObterAsync(c.TenantId,ct);} }

public sealed class RecebimentoCompraApplicationService(IRecebimentoCompraRepository repository):IRecebimentoCompraApplicationService
{
 public Task<CentralRecebimentos> ListarAsync(ComprasContext c,RecebimentoFiltro f,CancellationToken ct){ComprasGuard.Context(c);if(f.DataInicial.HasValue&&f.DataFinal.HasValue&&f.DataInicial>f.DataFinal)throw new ArgumentException("O período informado é inválido.");return repository.ListarAsync(c.TenantId,f,ct);}
 public Task<PedidoParaRecebimento?> ObterPedidoAsync(ComprasContext c,Guid id,CancellationToken ct){ComprasGuard.Context(c);if(id==Guid.Empty)throw new ArgumentException("Pedido inválido.");return repository.ObterPedidoAsync(c.TenantId,id,ct);}
 public Task<RecebimentoCriado> CriarEConfirmarAsync(ComprasContext c,CriarRecebimentoRequest r,string key,CancellationToken ct)
 {
  ComprasGuard.Context(c);ComprasGuard.Key(key);
  if(r.PedidoId==Guid.Empty||r.AlmoxarifadoId==Guid.Empty||r.PedidoVersion<=0)throw new ArgumentException("Pedido, destino e versão são obrigatórios.");
  if(string.IsNullOrWhiteSpace(r.Documento)||r.Documento.Length>100)throw new ArgumentException("Informe o documento do recebimento.");
  if(r.Itens.Count==0||r.Itens.Count>200||r.Itens.Any(i=>i.PedidoItemId<=0||i.Quantidade<=0||decimal.Round(i.Quantidade,4)!=i.Quantidade))throw new ArgumentException("Informe quantidades positivas com até quatro casas decimais.");
  if(r.Itens.GroupBy(i=>i.PedidoItemId).Any(g=>g.Count()>1))throw new ArgumentException("Cada item do pedido deve ser informado uma única vez.");
  if(r.Itens.Any(i=>i.Validade.HasValue&&!string.IsNullOrWhiteSpace(i.Lote)&&i.Validade.Value<DateOnly.FromDateTime(r.DataOperacao.UtcDateTime)))throw new ArgumentException("A validade do lote não pode ser anterior ao recebimento.");
  return repository.CriarEConfirmarAsync(c,r,key,ct);
 }
}
