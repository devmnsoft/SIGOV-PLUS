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
 public Task<CentralRecebimentos> ListarAsync(ComprasContext c,RecebimentoFiltro f,CancellationToken ct){ComprasGuard.Context(c);if(f.DataInicial.HasValue&&f.DataFinal.HasValue&&f.DataInicial>f.DataFinal)throw new ArgumentException("O período informado é inválido.");var status=string.IsNullOrWhiteSpace(f.Status)?null:f.Status.Trim().ToUpperInvariant();if(status is not null and not ("EM_CONFERENCIA" or "CONCLUIDO" or "COM_DIVERGENCIA" or "ESTORNADO"))throw new ArgumentException("A situação informada é inválida.");return repository.ListarAsync(c.TenantId,f with{Status=status},ct);}
 public Task<PedidoParaRecebimento?> ObterPedidoAsync(ComprasContext c,Guid id,CancellationToken ct){ComprasGuard.Context(c);if(id==Guid.Empty)throw new ArgumentException("Pedido inválido.");return repository.ObterPedidoAsync(c.TenantId,id,ct);}
 public Task<RecebimentoDetalhe?> ObterAsync(ComprasContext c,Guid id,CancellationToken ct){ComprasGuard.Context(c);if(id==Guid.Empty)throw new ArgumentException("Recebimento inválido.");return repository.ObterAsync(c.TenantId,id,ct);}
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
 public Task<RecebimentoCriado> ConcluirInspecaoAsync(ComprasContext c,Guid id,ConcluirInspecaoRequest r,CancellationToken ct)
 {
  ComprasGuard.Context(c);
  if(id==Guid.Empty||r.Version<=0)throw new ArgumentException("Recebimento ou versão inválida.");
  if(r.Itens.Count==0||r.Itens.Count>200||r.Itens.Any(i=>i.RecebimentoItemId<=0||i.QuantidadeAceita<0||i.QuantidadeRejeitada<0||decimal.Round(i.QuantidadeAceita,4)!=i.QuantidadeAceita||decimal.Round(i.QuantidadeRejeitada,4)!=i.QuantidadeRejeitada))throw new ArgumentException("Informe a decisão dos itens com valores não negativos e até quatro casas decimais.");
  if(r.Itens.GroupBy(i=>i.RecebimentoItemId).Any(g=>g.Count()>1))throw new ArgumentException("Cada item deve possuir uma única decisão.");
  if(r.Itens.Any(i=>i.QuantidadeRejeitada>0)&&string.IsNullOrWhiteSpace(r.Justificativa))throw new ArgumentException("A justificativa é obrigatória quando houver rejeição.");
  if(r.Justificativa?.Length>1000)throw new ArgumentException("A justificativa deve ter no máximo 1.000 caracteres.");
  return repository.ConcluirInspecaoAsync(c,id,r,ct);
 }
}

public sealed class DivergenciaRecebimentoApplicationService(IDivergenciaRecebimentoRepository repository):IDivergenciaRecebimentoApplicationService
{
 public Task<CentralDivergencias> ListarAsync(ComprasContext c,DivergenciaFiltro f,CancellationToken ct){ComprasGuard.Context(c);if(f.AberturaInicial>f.AberturaFinal||f.EncerramentoInicial>f.EncerramentoFinal)throw new ArgumentException("O período informado é inválido.");var s=string.IsNullOrWhiteSpace(f.Situacao)?null:f.Situacao.Trim().ToUpperInvariant();if(s is not null and not("ABERTA" or "EM_TRATAMENTO" or "ENCERRADA"))throw new ArgumentException("A situação informada é inválida.");return repository.ListarAsync(c.TenantId,f with{Situacao=s,Pagina=Math.Max(1,f.Pagina),Tamanho=Math.Clamp(f.Tamanho,1,100)},ct);}
 public Task<DivergenciaDetalhe?> ObterAsync(ComprasContext c,long id,CancellationToken ct){ComprasGuard.Context(c);if(id<=0)throw new ArgumentException("Divergência inválida.");return repository.ObterAsync(c.TenantId,id,ct);}
 public Task<IReadOnlyList<ResponsavelDivergencia>> PesquisarResponsaveisAsync(ComprasContext c,string? busca,int pagina,int tamanho,CancellationToken ct){ComprasGuard.Context(c);return repository.PesquisarResponsaveisAsync(c.TenantId,busca,Math.Max(1,pagina),Math.Clamp(tamanho,1,50),ct);}
 public Task<DivergenciaComandoResultado> AtribuirAsync(ComprasContext c,long id,AtribuirDivergenciaRequest r,CancellationToken ct){Command(c,id,r.Version,r.IdempotencyKey);if(r.ResponsavelId==Guid.Empty)throw new ArgumentException("Selecione um responsável elegível.");return repository.AtribuirAsync(c,id,r,ct);}
 public Task<DivergenciaComandoResultado> RegistrarAndamentoAsync(ComprasContext c,long id,RegistrarAndamentoDivergenciaRequest r,CancellationToken ct){Command(c,id,r.Version,r.IdempotencyKey);if(string.IsNullOrWhiteSpace(r.Descricao)||r.Descricao.Trim().Length<3||r.Descricao.Length>2000)throw new ArgumentException("O andamento deve ter entre 3 e 2.000 caracteres.");if(r.Providencia?.Length>40)throw new ArgumentException("A providência é inválida.");return repository.RegistrarAndamentoAsync(c,id,r with{Descricao=r.Descricao.Trim(),Providencia=Clean(r.Providencia)},ct);}
 public Task<DivergenciaComandoResultado> EncerrarAsync(ComprasContext c,long id,EncerrarDivergenciaRequest r,CancellationToken ct){Command(c,id,r.Version,r.IdempotencyKey);if(string.IsNullOrWhiteSpace(r.Resultado)||r.Resultado.Trim().Length<3||r.Resultado.Length>2000)throw new ArgumentException("O resultado deve ter entre 3 e 2.000 caracteres.");if(string.IsNullOrWhiteSpace(r.Justificativa)||r.Justificativa.Trim().Length<10||r.Justificativa.Length>2000)throw new ArgumentException("A justificativa de encerramento deve ter entre 10 e 2.000 caracteres.");return repository.EncerrarAsync(c,id,r with{Resultado=r.Resultado.Trim(),Justificativa=r.Justificativa.Trim()},ct);}
 private static void Command(ComprasContext c,long id,long version,string key){ComprasGuard.Context(c);ComprasGuard.Key(key);if(id<=0||version<=0)throw new ArgumentException("Divergência ou versão inválida.");}
 private static string? Clean(string? value)=>string.IsNullOrWhiteSpace(value)?null:value.Trim().ToUpperInvariant();
}

public sealed class DevolucaoCompraApplicationService(IDevolucaoCompraRepository repository):IDevolucaoCompraApplicationService
{
 public Task<Common.PagedResult<DevolucaoResumo>> ListarAsync(Guid tenant,DevolucaoFiltro f,CancellationToken ct)=>repository.ListarAsync(tenant,f with{Pagina=Math.Max(1,f.Pagina),Tamanho=Math.Clamp(f.Tamanho,1,100)},ct);
 public Task<DevolucaoDetalhe?> ObterAsync(Guid tenant,long id,CancellationToken ct)=>repository.ObterAsync(tenant,id,ct);
 public Task<OrigemDevolucao?> ObterOrigemAsync(Guid tenant,Guid recebimentoId,CancellationToken ct)=>repository.ObterOrigemAsync(tenant,recebimentoId,ct);
 public Task<DevolucaoComandoResultado> CriarAsync(ComprasContext c,CriarDevolucaoRequest r,CancellationToken ct){Validate(c,r.IdempotencyKey,r.Motivo,r.OrigemFisica,r.Destino,r.Itens);if(r.ResponsavelId==Guid.Empty)throw new ArgumentException("Selecione o responsável.");if(r.EsferaGoverno is not("municipal" or "estadual" or "federal"))throw new ArgumentException("Esfera de governo inválida.");return repository.CriarAsync(c,r,ct);}
 public Task<DevolucaoComandoResultado> EditarAsync(ComprasContext c,long id,EditarDevolucaoRequest r,CancellationToken ct){Validate(c,r.IdempotencyKey,r.Motivo,r.OrigemFisica,r.Destino,r.Itens);Version(id,r.Version);return repository.EditarAsync(c,id,r,ct);}
 public Task<DevolucaoComandoResultado> ExpedirAsync(ComprasContext c,long id,ExpedirDevolucaoRequest r,CancellationToken ct){Command(c,id,r.Version,r.IdempotencyKey);if(r.SaidaEm==default||r.Modalidade is not("TRANSPORTADORA" or "ENTREGA_DIRETA"))throw new ArgumentException("Informe data e modalidade da saída.");if(r.Modalidade=="TRANSPORTADORA"&&string.IsNullOrWhiteSpace(r.Transportadora))throw new ArgumentException("Informe a transportadora para esta modalidade.");return repository.ExpedirAsync(c,id,r,ct);}
 public Task<DevolucaoComandoResultado> EntregarAsync(ComprasContext c,long id,EntregarDevolucaoRequest r,CancellationToken ct){Command(c,id,r.Version,r.IdempotencyKey);if(r.EntregueEm==default||string.IsNullOrWhiteSpace(r.RecebedorOuReferencia)||string.IsNullOrWhiteSpace(r.DocumentoProtocolo))throw new ArgumentException("Data, recebedor/referência e protocolo são obrigatórios.");return repository.EntregarAsync(c,id,r,ct);}
 public Task<DevolucaoComandoResultado> CancelarAsync(ComprasContext c,long id,CancelarDevolucaoRequest r,CancellationToken ct){Command(c,id,r.Version,r.IdempotencyKey);if(string.IsNullOrWhiteSpace(r.Justificativa)||r.Justificativa.Trim().Length<10)throw new ArgumentException("A justificativa deve ter ao menos 10 caracteres.");return repository.CancelarAsync(c,id,r,ct);}
 private static void Validate(ComprasContext c,string key,string motivo,string origem,string destino,IReadOnlyList<DevolucaoItemInput> itens){ComprasGuard.Context(c);ComprasGuard.Key(key);if(string.IsNullOrWhiteSpace(motivo)||string.IsNullOrWhiteSpace(origem)||string.IsNullOrWhiteSpace(destino))throw new ArgumentException("Motivo, origem física e destino são obrigatórios.");if(itens.Count==0||itens.Any(x=>x.Quantidade<=0||decimal.Round(x.Quantidade,4)!=x.Quantidade)||itens.GroupBy(x=>x.RecebimentoItemId).Any(x=>x.Count()>1))throw new ArgumentException("Informe itens únicos com quantidade positiva e até quatro casas decimais.");}
 private static void Command(ComprasContext c,long id,long version,string key){ComprasGuard.Context(c);ComprasGuard.Key(key);Version(id,version);} private static void Version(long id,long version){if(id<=0||version<=0)throw new ArgumentException("Devolução ou versão inválida.");}
}
