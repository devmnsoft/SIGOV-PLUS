using System.Text;
using System.Text.Json;
using Dapper;
using Npgsql;
using Sigov.Application.Common;
using Sigov.Application.Educacao.Avancada;
using Sigov.Application.Health;
using Sigov.Infrastructure.Persistence.Dapper;
namespace Sigov.Infrastructure.Educacao.Avancada;
public sealed class EducacaoAvancadaRepository:IEducacaoTransporteEscolarRepository,IEducacaoTransporteDashboardService,IEducacaoTransporteRelatorioService,IEducacaoMerendaRepository,IEducacaoMerendaEstoqueService,IEducacaoMerendaCardapioService,IEducacaoMerendaRelatorioService,IEducacaoBibliotecaRepository,IEducacaoBibliotecaEmprestimoService,IEducacaoBibliotecaRelatorioService,IEducacaoIndicadoresRepository,IEducacaoFundebService,IEducacaoCustosService,IEducacaoEducacensoService,IEducacaoAvaliacaoMagisterioService
{
  private sealed record EducacaoTotais(long Total,long Ativos,long Pendentes,long Alertas);
  private sealed class EducacaoCardapioLinha{public long Id{get;set;} public long? EntidadeId{get;set;} public long? EscolaId{get;set;} public string? Status{get;set;} public DateOnly? Inicio{get;set;} public DateOnly? Fim{get;set;}}
 static readonly HashSet<string> Recursos=new(StringComparer.Ordinal){"educacao_transporte_rota","educacao_transporte_rota_ponto","educacao_transporte_veiculo","educacao_transporte_motorista","educacao_transporte_aluno","educacao_transporte_viagem","educacao_transporte_frequencia","educacao_transporte_ocorrencia","educacao_transporte_custo","educacao_merenda_produto","educacao_merenda_fornecedor","educacao_merenda_estoque","educacao_merenda_movimento","educacao_merenda_cardapio","educacao_merenda_distribuicao","educacao_merenda_requisicao","educacao_merenda_consumo","educacao_biblioteca_acervo","educacao_biblioteca_exemplar","educacao_biblioteca_emprestimo","educacao_biblioteca_reserva","educacao_biblioteca_ocorrencia","educacao_biblioteca_digital_referencia","educacao_fundeb_parametro","educacao_fundeb_receita","educacao_fundeb_aplicacao","educacao_custo_centro","educacao_custo_lancamento","educacao_custo_rateio","educacao_indicador","educacao_indicador_valor","educacao_projecao_matricula","educacao_educacenso_lote","educacao_educacenso_inconsistencia","educacao_avaliacao_magisterio","educacao_avaliacao_resultado"};
 readonly DapperContext _context; readonly IDatabaseObjectInspector _inspector; public EducacaoAvancadaRepository(DapperContext context,IDatabaseObjectInspector inspector){_context=context;_inspector=inspector;}
 public async Task<PagedResult<EducacaoAvancadaRegistroDto>> ListarAsync(long tenantId,string recurso,EducacaoAvancadaFiltro f,CancellationToken ct){recurso=Recurso(recurso);var p=Math.Max(1,f.Pagina);var t=Math.Clamp(f.Tamanho,1,100);if(!await _inspector.TableExistsAsync("sigov",recurso,ct).ConfigureAwait(false))return PagedResult<EducacaoAvancadaRegistroDto>.Indisponivel(p,t,"Recurso 'sigov."+recurso+"' indisponível nesta instância: schema ausente (instalação/migration pendente), não ausência de dados.");var sql=$"select id as Id,codigo as Codigo,nome as Nome,status as Status,tipo as Tipo,descricao as Descricao,quantidade as Quantidade,valor as Valor,created_at as \"CreatedAt\",coalesce(updated_at,created_at) as \"UpdatedAt\" from sigov.{recurso} where tenant_id=@TenantId and is_deleted=false and (@EscolaId is null or escola_id=@EscolaId) and (@AnoLetivo is null or ano_letivo=@AnoLetivo) and (@Status is null or status=@Status) order by id desc limit @Tamanho offset @Offset;select count(1) from sigov.{recurso} where tenant_id=@TenantId and is_deleted=false and (@EscolaId is null or escola_id=@EscolaId) and (@AnoLetivo is null or ano_letivo=@AnoLetivo) and (@Status is null or status=@Status)";using var c=_context.CreateConnection();using var m=await c.QueryMultipleAsync(new CommandDefinition(sql,new{TenantId=tenantId,f.EscolaId,f.AnoLetivo,f.Status,Tamanho=t,Offset=(p-1)*t},cancellationToken:ct));var rows=(await m.ReadAsync<EducacaoAvancadaRegistroDto>()).ToList();return new(rows,p,t,await m.ReadSingleAsync<long>());}
 public async Task<long> CriarAsync(EducacaoAvancadaContext x,string recurso,EducacaoAvancadaOperacaoRequest r,CancellationToken ct){recurso=Recurso(recurso);Validar(recurso,r);await ValidarEscopoInstitucionalAsync(x.TenantId,x.EntidadeId,r.EscolaId,r.AnoLetivo,ct).ConfigureAwait(false);using var c=_context.CreateConnection();var sql=$"insert into sigov.{recurso}(tenant_id,entidade_id,exercicio_id,ano_letivo,escola_id,aluno_id,referencia_id,codigo,nome,tipo,status,descricao,justificativa,turno,quantidade,valor,dados,auditoria,correlation_id,created_by) values(@TenantId,@EntidadeId,@ExercicioId,@AnoLetivo,@EscolaId,@AlunoId,@ReferenciaId,@Codigo,@Nome,@Tipo,@Status,@Descricao,@Justificativa,@Turno,@Quantidade,@Valor,@Dados::jsonb,jsonb_build_array(jsonb_build_object('acao','CRIAR','em',now())),@CorrelationId,@UsuarioId) returning id";return await c.ExecuteScalarAsync<long>(new CommandDefinition(sql,new{x.TenantId,x.EntidadeId,x.ExercicioId,r.AnoLetivo,r.EscolaId,r.AlunoId,r.ReferenciaId,r.Codigo,r.Nome,r.Tipo,r.Status,r.Descricao,r.Justificativa,r.Turno,r.Quantidade,r.Valor,Dados=JsonSerializer.Serialize(r.Dados??new Dictionary<string,object?>()),CorrelationId=Guid.TryParse(x.CorrelationId,out var g)?g:Guid.NewGuid(),x.UsuarioId},cancellationToken:ct));}
 // Ciclo 1 Sprint 5 (merenda base): cardápio tipado com produtos — escopo institucional da escola,
 // produtos válidos do tenant, unicidade de período vigente (escola × data de início; backstop no
 // índice ux_merenda_cardapio_tenant_escola_inicio) e itens persistidos transacionalmente com
 // referência ao cardápio. Falhas nomeadas (regra 13); multi-esfera (regra 24).
 public async Task<long> CriarCardapioComItensAsync(EducacaoAvancadaContext x,EducacaoMerendaCriarCardapioRequest r,CancellationToken ct){
   if(!await _inspector.TableExistsAsync("sigov","educacao_merenda_cardapio",ct).ConfigureAwait(false))throw new RecursoNaoDisponivelException("educacao_merenda_cardapio");
   if(r is null)throw new ArgumentException("Cardápio é obrigatório.");
   if(r.EscolaId<=0)throw new ArgumentException("Cardápio exige escola.");
   if(r.Fim<r.Inicio)throw new ArgumentException("O fim do cardápio deve ser posterior ou igual ao início.");
   if(r.Produtos is null||r.Produtos.Count==0)throw new ArgumentException("Cardápio exige ao menos um produto.");
   var vistos=new HashSet<long>();
   foreach(var p in r.Produtos){if(p<=0)throw new ArgumentException("Produto inválido no cardápio.");if(!vistos.Add(p))throw new CardapioProdutoDuplicadoException(p);}
   await ValidarEscopoInstitucionalAsync(x.TenantId,x.EntidadeId,r.EscolaId,null,ct).ConfigureAwait(false);
   using var c=(NpgsqlConnection)_context.CreateConnection();
   await c.OpenAsync(ct).ConfigureAwait(false);
   await using var tx=await c.BeginTransactionAsync(ct).ConfigureAwait(false);
   foreach(var p in vistos){
     var ok=await c.ExecuteScalarAsync<bool>(new CommandDefinition("select exists(select 1 from sigov.educacao_merenda_produto where tenant_id=@TenantId and id=@Id and ativo and not is_deleted)",new{TenantId=x.TenantId,Id=p},tx,cancellationToken:ct)).ConfigureAwait(false);
     if(!ok)throw new CardapioProdutoInexistenteException(p);
   }
   var periodoOcupado=await c.ExecuteScalarAsync<bool>(new CommandDefinition("select exists(select 1 from sigov.educacao_merenda_cardapio where tenant_id=@TenantId and escola_id=@EscolaId and data_inicio=@Inicio and is_deleted=false and status not in ('ENCERRADA','ENCERRADO','CANCELADA','CANCELADO'))",new{TenantId=x.TenantId,EscolaId=r.EscolaId,Inicio=r.Inicio},tx,cancellationToken:ct)).ConfigureAwait(false);
   if(periodoOcupado)throw new CardapioPeriodoExistenteException(r.EscolaId,r.Inicio);
   var corr=Guid.TryParse(x.CorrelationId,out var g)?g:Guid.NewGuid();
   var cid=await c.ExecuteScalarAsync<long>(new CommandDefinition("insert into sigov.educacao_merenda_cardapio(tenant_id,entidade_id,escola_id,data_inicio,data_fim,nome,tipo,status,dados,auditoria,correlation_id,created_by) values(@TenantId,@EntidadeId,@EscolaId,@Inicio,@Fim,@Nome,'CARDAPIO_MERENDA','RASCUNHO','{}'::jsonb,jsonb_build_array(jsonb_build_object('acao','CRIAR','em',now())),@CorrelationId,@UsuarioId) returning id",new{TenantId=x.TenantId,EntidadeId=x.EntidadeId,EscolaId=r.EscolaId,Inicio=r.Inicio,Fim=r.Fim,Nome="Cardápio " + r.Inicio.ToString("yyyy-MM-dd") + " a " + r.Fim.ToString("yyyy-MM-dd"),CorrelationId=corr,UsuarioId=x.UsuarioId},tx,cancellationToken:ct)).ConfigureAwait(false);
   foreach(var p in vistos){
     var n=await c.ExecuteAsync(new CommandDefinition("insert into sigov.educacao_merenda_cardapio_item(tenant_id,entidade_id,escola_id,referencia_id,produto_id,nome,unidade,status,dados,auditoria,correlation_id,created_by) select @TenantId,@EntidadeId,@EscolaId,@CardapioId,p.id,p.nome,p.unidade,'RASCUNHO','{}'::jsonb,jsonb_build_array(jsonb_build_object('acao','CRIAR','em',now())),@CorrelationId,@UsuarioId from sigov.educacao_merenda_produto p where p.tenant_id=@TenantId and p.id=@Id and p.ativo and not p.is_deleted",new{TenantId=x.TenantId,EntidadeId=x.EntidadeId,EscolaId=r.EscolaId,CardapioId=cid,Id=p,CorrelationId=corr,UsuarioId=x.UsuarioId},tx,cancellationToken:ct)).ConfigureAwait(false);
     if(n!=1)throw new CardapioProdutoInexistenteException(p);
   }
   await tx.CommitAsync(ct).ConfigureAwait(false);
   return cid;
 }
 // Sprint 6 (merenda operação): registro de estoque por produto — upsert do saldo
 // (sem linha cria; com linha adiciona o ajuste). Produto deve existir e estar ativo.
 // Falha nomeada (regra 13); multi-esfera (regra 24).
 public async Task<long> RegistrarEstoqueAsync(EducacaoAvancadaContext x,EducacaoMerendaRegistrarEstoqueRequest r,CancellationToken ct){
   if(!await _inspector.TableExistsAsync("sigov","educacao_merenda_estoque",ct).ConfigureAwait(false))throw new RecursoNaoDisponivelException("educacao_merenda_estoque");
   if(r is null)throw new ArgumentException("Registro de estoque é obrigatório.");
   if(r.ProdutoId<=0)throw new ArgumentException("Registro de estoque exige produto.");
   var ok=await _context.CreateConnection().ExecuteScalarAsync<bool>(new CommandDefinition("select exists(select 1 from sigov.educacao_merenda_produto where tenant_id=@TenantId and id=@Id and ativo and not is_deleted)",new{TenantId=x.TenantId,Id=r.ProdutoId},cancellationToken:ct)).ConfigureAwait(false);
   if(!ok)throw new MerendaProdutoInexistenteException(r.ProdutoId);
   var corr=Guid.TryParse(x.CorrelationId,out var g)?g:Guid.NewGuid();
   var existente=await _context.CreateConnection().ExecuteScalarAsync<long>(new CommandDefinition("select coalesce(min(id),0) from sigov.educacao_merenda_estoque where tenant_id=@TenantId and produto_id=@Id and is_deleted=false",new{TenantId=x.TenantId,Id=r.ProdutoId},cancellationToken:ct)).ConfigureAwait(false);
   if(existente>0)return await _context.CreateConnection().ExecuteScalarAsync<long>(new CommandDefinition("update sigov.educacao_merenda_estoque set saldo=coalesce(saldo,0)+@Delta,updated_at=now() where id=@Id returning id",new{Delta=r.SaldoInicial,Id=existente},cancellationToken:ct)).ConfigureAwait(false);
   return await _context.CreateConnection().ExecuteScalarAsync<long>(new CommandDefinition(@"insert into sigov.educacao_merenda_estoque(tenant_id,entidade_id,produto_id,nome,unidade,saldo,saldo_minimo,status,dados,auditoria,correlation_id,created_by)
select @TenantId,@EntidadeId,p.id,p.nome,p.unidade,@SaldoInicial,0,'ATIVA','{}'::jsonb,jsonb_build_array(jsonb_build_object('acao','REGISTRAR_ESTOQUE','em',now())),@CorrelationId,@UsuarioId
from sigov.educacao_merenda_produto p where p.tenant_id=@TenantId and p.id=@Id and p.ativo and not p.is_deleted
returning id",new{TenantId=x.TenantId,EntidadeId=x.EntidadeId,Id=r.ProdutoId,SaldoInicial=r.SaldoInicial,CorrelationId=corr,UsuarioId=x.UsuarioId},cancellationToken:ct)).ConfigureAwait(false);
 }
 // Sprint 6 (merenda operação): distribuição de refeição — cardápio ATIVO dentro do
 // período, itens restritos ao menu, estoque suficiente (lock por produto em ordem
 // determinística para evitar deadlock), distribuição + itens + movimentos de saída
 // + baixa de saldo numérica na mesma transação. Backstop no índice
 // ux_merenda_distribuicao_tenant_cardapio_data. Falhas nomeadas (regra 13).
 public async Task<long> DistribuirRefeicaoAsync(EducacaoAvancadaContext x,EducacaoMerendaDistribuirRefeicaoRequest r,CancellationToken ct){
   if(!await _inspector.TableExistsAsync("sigov","educacao_merenda_distribuicao",ct).ConfigureAwait(false))throw new RecursoNaoDisponivelException("educacao_merenda_distribuicao");
   if(r is null)throw new ArgumentException("Distribuição é obrigatória.");
   if(r.CardapioId<=0)throw new ArgumentException("Distribuição exige cardápio.");
   if(r.Itens is null||r.Itens.Count==0)throw new ArgumentException("Distribuição exige ao menos um item.");
   var itens=new List<EducacaoMerendaDistribuirItemRequest>();
   var vistos=new HashSet<long>();
   foreach(var it in r.Itens){if(it is null||it.ProdutoId<=0)throw new ArgumentException("Item inválido na distribuição.");if(it.Quantidade<=0)throw new ArgumentException("Quantidade do item deve ser maior que zero.");if(!vistos.Add(it.ProdutoId))throw new ArgumentException("Produto repetido na distribuição.");itens.Add(it);}
   using var c=(NpgsqlConnection)_context.CreateConnection();
   await c.OpenAsync(ct).ConfigureAwait(false);
   var card=await c.QueryFirstOrDefaultAsync<EducacaoCardapioLinha>(new CommandDefinition("select id as Id,entidade_id as EntidadeId,escola_id as EscolaId,status as Status,data_inicio as Inicio,data_fim as Fim from sigov.educacao_merenda_cardapio where tenant_id=@TenantId and id=@Id and is_deleted=false",new{TenantId=x.TenantId,Id=r.CardapioId},cancellationToken:ct)).ConfigureAwait(false);
   if(card is null||card.Id==0)throw new DistribuicaoCardapioInexistenteException(r.CardapioId);
   if(x.EntidadeId.HasValue&&card.EntidadeId.HasValue&&card.EntidadeId.Value!=x.EntidadeId.Value)throw new CardapioForaDoEscopoException(r.CardapioId,card.EntidadeId.Value,x.EntidadeId.Value);
   if(card.Status is not ("ATIVA" or "ATIVO"))throw new CardapioNaoAprovadoException(r.CardapioId,card.Status ?? "INDEFINIDO");
   var ini=card.Inicio;var fim=card.Fim;
   if(ini==null||fim==null)throw new DataForaDoPeriodoException(r.CardapioId,r.Data,r.Data,r.Data);
   if(r.Data<ini||r.Data>fim)throw new DataForaDoPeriodoException(r.CardapioId,r.Data,ini.Value,fim.Value);
   var noMenu=await c.ExecuteScalarAsync<int>(new CommandDefinition("select count(distinct u.p) from unnest(@Ids) as u(p) where exists(select 1 from sigov.educacao_merenda_cardapio_item i where i.referencia_id=@CardapioId and i.produto_id=u.p and i.is_deleted=false)",new{Ids=itens.Select(i=>i.ProdutoId).ToArray(),CardapioId=r.CardapioId},cancellationToken:ct)).ConfigureAwait(false);
   if(noMenu!=itens.Count){
     long fora=0;
     foreach(var it in itens){
       var m=await c.ExecuteScalarAsync<bool>(new CommandDefinition("select exists(select 1 from sigov.educacao_merenda_cardapio_item i where i.referencia_id=@CardapioId and i.produto_id=@P and i.is_deleted=false)",new{CardapioId=r.CardapioId,P=it.ProdutoId},cancellationToken:ct)).ConfigureAwait(false);
       if(!m){fora=it.ProdutoId;break;}
     }
     throw new ProdutoForaDoMenuException(fora,r.CardapioId);
   }
   await using var tx=await c.BeginTransactionAsync(ct).ConfigureAwait(false);
   var diaOcupado=await c.ExecuteScalarAsync<bool>(new CommandDefinition("select exists(select 1 from sigov.educacao_merenda_distribuicao where tenant_id=@TenantId and referencia_id=@CardapioId and competencia=@Data and is_deleted=false and status not in ('ENCERRADA','ENCERRADO','CANCELADA','CANCELADO'))",new{TenantId=x.TenantId,CardapioId=r.CardapioId,Data=r.Data},tx,cancellationToken:ct)).ConfigureAwait(false);
   if(diaOcupado)throw new DistribuicaoDiaExistenteException(r.CardapioId,r.Data);
   var corr=Guid.TryParse(x.CorrelationId,out var g2)?g2:Guid.NewGuid();
   foreach(var it in itens.OrderBy(i=>i.ProdutoId)){
     var est=await c.QueryFirstOrDefaultAsync<(long Id,decimal Saldo)>(new CommandDefinition("select id,coalesce(saldo,0) from sigov.educacao_merenda_estoque where tenant_id=@TenantId and produto_id=@P and is_deleted=false order by id limit 1 for update",new{TenantId=x.TenantId,P=it.ProdutoId},tx,cancellationToken:ct)).ConfigureAwait(false);
     var disponivel=est.Id>0?est.Saldo:0m;
     if(disponivel<it.Quantidade)throw new EstoqueInsuficienteException(it.ProdutoId,disponivel,it.Quantidade);
   }
   var did=await c.ExecuteScalarAsync<long>(new CommandDefinition("insert into sigov.educacao_merenda_distribuicao(tenant_id,entidade_id,escola_id,referencia_id,competencia,tipo,status,dados,auditoria,correlation_id,created_by) values(@TenantId,@EntidadeId,@EscolaId,@CardapioId,@Data,'REFEICAO','ATIVA','{}'::jsonb,jsonb_build_array(jsonb_build_object('acao','DISTRIBUIR_REFEICAO','em',now())),@CorrelationId,@UsuarioId) returning id",new{TenantId=x.TenantId,EntidadeId=x.EntidadeId,EscolaId=card.EscolaId,CardapioId=r.CardapioId,Data=r.Data,CorrelationId=corr,UsuarioId=x.UsuarioId},tx,cancellationToken:ct)).ConfigureAwait(false);
   foreach(var it in itens){
     await c.ExecuteAsync(new CommandDefinition("insert into sigov.educacao_merenda_distribuicao_item(tenant_id,entidade_id,escola_id,referencia_id,produto_id,nome,unidade,quantidade,competencia,status,dados,auditoria,correlation_id,created_by) select @TenantId,@EntidadeId,@EscolaId,@DistribuidaoId,p.id,p.nome,p.unidade,@Qtd,@Data,'ATIVA','{}'::jsonb,jsonb_build_array(jsonb_build_object('acao','DISTRIBUIR_REFEICAO','em',now())),@CorrelationId,@UsuarioId from sigov.educacao_merenda_produto p where p.tenant_id=@TenantId and p.id=@Id and p.ativo and not p.is_deleted",new{TenantId=x.TenantId,EntidadeId=x.EntidadeId,EscolaId=card.EscolaId,DistribuidaoId=did,Id=it.ProdutoId,Qtd=it.Quantidade,Data=r.Data,CorrelationId=corr,UsuarioId=x.UsuarioId},tx,cancellationToken:ct)).ConfigureAwait(false);
     await c.ExecuteAsync(new CommandDefinition("insert into sigov.educacao_merenda_movimento(tenant_id,entidade_id,referencia_id,produto_id,tipo,quantidade,competencia,status,dados,auditoria,correlation_id,created_by) values(@TenantId,@EntidadeId,@DistribuidaoId,@Id,'SAIDA',-@Qtd,@Data,'ATIVA','{}'::jsonb,jsonb_build_array(jsonb_build_object('acao','BAIXA_DISTRIBUICAO','em',now())),@CorrelationId,@UsuarioId)",new{TenantId=x.TenantId,EntidadeId=x.EntidadeId,DistribuidaoId=did,Id=it.ProdutoId,Qtd=it.Quantidade,Data=r.Data,CorrelationId=corr,UsuarioId=x.UsuarioId},tx,cancellationToken:ct)).ConfigureAwait(false);
     await c.ExecuteAsync(new CommandDefinition("update sigov.educacao_merenda_estoque set saldo=coalesce(saldo,0)-@Qtd,updated_at=now() where tenant_id=@TenantId and produto_id=@Id and is_deleted=false",new{TenantId=x.TenantId,Id=it.ProdutoId,Qtd=it.Quantidade},tx,cancellationToken:ct)).ConfigureAwait(false);
   }
   await tx.CommitAsync(ct).ConfigureAwait(false);
   return did;
 }
 public async Task<bool> AlterarStatusAsync(EducacaoAvancadaContext x,string recurso,long id,string status,string? justificativa,CancellationToken ct,DateTime? versaoAtual=null){recurso=Recurso(recurso);if(string.IsNullOrWhiteSpace(status))throw new ArgumentException("Status é obrigatório.");var alvo=status.Trim().ToUpperInvariant();if ((alvo is "CANCELADA" or "CANCELADO") && string.IsNullOrWhiteSpace(justificativa))throw new ArgumentException("Cancelamento exige justificativa.");if(!await _inspector.TableExistsAsync("sigov",recurso,ct).ConfigureAwait(false))throw new RecursoNaoDisponivelException(recurso);using var c=_context.CreateConnection();var de=await c.QueryFirstOrDefaultAsync<(string? Status,long LinhaEntidade)>(new CommandDefinition($"select status,coalesce(entidade_id,-1) as LinhaEntidade from sigov.{recurso} where tenant_id=@TenantId and id=@Id and is_deleted=false",new{x.TenantId,Id=id},cancellationToken:ct));if(de.Status is null)throw new RegistroNaoEncontradoException(recurso,id);if(de.LinhaEntidade>=0 && x.EntidadeId.HasValue && x.EntidadeId.Value!=de.LinhaEntidade)throw new ContextoInstitucionalDivergenteException(recurso,id,de.LinhaEntidade,x.EntidadeId.Value);var linhaDe=de.Status;if(!EducacaoStatusMaquina.TransicaoPermitida(linhaDe,alvo))throw new TransicaoDeStatusInvalidaException(linhaDe,alvo);var n=await c.ExecuteAsync(new CommandDefinition($"update sigov.{recurso} set status=@Status,justificativa=coalesce(@Justificativa,justificativa),updated_at=now(),updated_by=@UsuarioId,correlation_id=@CorrelationId,auditoria=(case jsonb_typeof(auditoria) when 'array' then auditoria when 'object' then case when auditoria='{{}}'::jsonb then '[]'::jsonb else jsonb_build_array(auditoria) end else '[]'::jsonb end)||jsonb_build_array(jsonb_build_object('acao','STATUS','de',@De,'para',@Status,'motivo',coalesce(@Justificativa,justificativa),'em',now())) where tenant_id=@TenantId and id=@Id and is_deleted=false and (@VersaoAtual::timestamptz is null or coalesce(updated_at,created_at)=@VersaoAtual)",new{x.TenantId,Id=id,Status=alvo,De=linhaDe,Justificativa=justificativa,x.UsuarioId,CorrelationId=Guid.TryParse(x.CorrelationId,out var g)?g:Guid.NewGuid(),VersaoAtual=versaoAtual},cancellationToken:ct));if(n==0)throw new ConflitoDeVersaoEducacaoException(recurso,id);return true;}
 public async Task<EducacaoAvancadaDashboardDto> DashboardAsync(long tenantId,string recurso,CancellationToken ct){recurso=Recurso(recurso);if(!await _inspector.TableExistsAsync("sigov",recurso,ct).ConfigureAwait(false))return new EducacaoAvancadaDashboardDto(0,0,0,0,Array.Empty<EducacaoAvancadaRegistroDto>());using var c=_context.CreateConnection();var sql=$"select count(1) as total,count(1) filter (where status in ('ATIVA','ATIVO','PUBLICADO','DISPONIVEL','ABERTO')) as ativos,count(1) filter (where status in ('RASCUNHO','PROGRAMADA','SOLICITADA')) as pendentes,count(1) filter (where status in ('ATRASADO','SUSPENSA','COM_INCONSISTENCIA')) as alertas from sigov.{recurso} where tenant_id=@TenantId and is_deleted=false;select id as Id,codigo as Codigo,nome as Nome,status as Status,tipo as Tipo,descricao as Descricao,quantidade as Quantidade,valor as Valor,created_at as \"CreatedAt\",coalesce(updated_at,created_at) as \"UpdatedAt\" from sigov.{recurso} where tenant_id=@TenantId and is_deleted=false order by id desc limit 6;";using var m=await c.QueryMultipleAsync(new CommandDefinition(sql,new{TenantId=tenantId},cancellationToken:ct));var tot=await m.ReadFirstAsync<EducacaoTotais>();var recentes=(await m.ReadAsync<EducacaoAvancadaRegistroDto>()).ToList();return new(tot.Total,tot.Ativos,tot.Pendentes,tot.Alertas,recentes);}
 public async Task<byte[]> ExportarCsvAsync(long tenantId,string recurso,CancellationToken ct){recurso=Recurso(recurso);if(!await _inspector.TableExistsAsync("sigov",recurso,ct).ConfigureAwait(false))return System.Text.Encoding.UTF8.GetPreamble().Concat(System.Text.Encoding.UTF8.GetBytes("id;codigo;nome;tipo;status;valor\n")).ToArray();using var c=_context.CreateConnection();var sql=$"select id as Id,codigo as Codigo,nome as Nome,status as Status,tipo as Tipo,descricao as Descricao,quantidade as Quantidade,valor as Valor,created_at as \"CreatedAt\",coalesce(updated_at,created_at) as \"UpdatedAt\" from sigov.{recurso} where tenant_id=@TenantId and is_deleted=false order by id";var r=(await c.QueryAsync<EducacaoAvancadaRegistroDto>(new CommandDefinition(sql,new{TenantId=tenantId},cancellationToken:ct))).ToList();var b=new StringBuilder("id;codigo;nome;tipo;status;valor\n");foreach(var x in r){b.Append(x.Id).Append(';').Append(EducacaoCsv.Campo(x.Codigo)).Append(';').Append(EducacaoCsv.Campo(x.Nome)).Append(';').Append(EducacaoCsv.Campo(x.Tipo)).Append(';').Append(EducacaoCsv.Campo(x.Status)).Append(';').Append(x.Valor?.ToString(System.Globalization.CultureInfo.InvariantCulture)).Append('\n');}return System.Text.Encoding.UTF8.GetPreamble().Concat(System.Text.Encoding.UTF8.GetBytes(b.ToString())).ToArray();}
 async Task ValidarEscopoInstitucionalAsync(long tenantId,long? entidadeId,long? escolaId,int? anoLetivo,CancellationToken ct){
    if(escolaId is null && anoLetivo is null)return;
    using var c=_context.CreateConnection();
    if(escolaId.HasValue){
        var ent=await c.ExecuteScalarAsync<long?>(new CommandDefinition("select entidade_id from sigov.escola where id=@Id and tenant_id=@TenantId and is_deleted=false",new{Id=escolaId.Value,TenantId=tenantId},cancellationToken:ct));
        if(ent is null)throw new EscolaInexistenteException(escolaId.Value);
        if(entidadeId.HasValue && entidadeId.Value!=ent.Value)throw new EscolaForaDoEscopoException(escolaId.Value,ent.Value,entidadeId.Value);
    }
    if(anoLetivo.HasValue){
        var ok=await c.ExecuteScalarAsync<bool>(new CommandDefinition("select exists(select 1 from sigov.ano_letivo where tenant_id=@TenantId and ano=@AnoLetivo and is_deleted=false and (@EntidadeId is null or entidade_id=@EntidadeId) and (@EscolaId is null or escola_id is null or escola_id=@EscolaId))",new{TenantId=tenantId,AnoLetivo=anoLetivo.Value,EntidadeId=entidadeId,EscolaId=escolaId},cancellationToken:ct));
        if(!ok)throw new AnoLetivoInexistenteException(anoLetivo.Value);
    }
}
static void Validar(string recurso,EducacaoAvancadaOperacaoRequest r){if(string.IsNullOrWhiteSpace(r.Status))throw new ArgumentException("Status é obrigatório.");if(recurso=="educacao_transporte_rota"&&(string.IsNullOrWhiteSpace(r.Nome)||string.IsNullOrWhiteSpace(r.Turno)||(!r.EscolaId.HasValue&&!(r.Dados?.ContainsKey("regiao")??false))))throw new ArgumentException("Rota exige escola ou região, nome e turno.");if(recurso=="educacao_merenda_produto"&&(string.IsNullOrWhiteSpace(r.Nome)||string.IsNullOrWhiteSpace(r.Tipo)))throw new ArgumentException("Produto exige nome, unidade e categoria.");if(recurso=="educacao_biblioteca_acervo"&&(string.IsNullOrWhiteSpace(r.Nome)||string.IsNullOrWhiteSpace(r.Tipo)))throw new ArgumentException("Acervo exige título e tipo.");if(recurso=="educacao_merenda_movimento"&&r.Quantidade<=0)throw new ArgumentException("Movimento exige quantidade positiva.");if(recurso=="educacao_custo_rateio"&&string.IsNullOrWhiteSpace(r.Tipo))throw new ArgumentException("Rateio exige critério.");}
 static string Recurso(string r)=>Recursos.Contains(r)?r:throw new ArgumentException("Recurso de educação avançada inválido.");
}
