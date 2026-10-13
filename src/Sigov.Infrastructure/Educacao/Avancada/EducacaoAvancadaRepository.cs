using System.Text;
using System.Text.Json;
using Dapper;
using Npgsql;
using Sigov.Application.Abstractions;
using Sigov.Application.Common;
using Sigov.Application.Educacao.Avancada;
using Sigov.Application.Health;
using Sigov.Infrastructure.Persistence.Dapper;
namespace Sigov.Infrastructure.Educacao.Avancada;
public sealed class EducacaoAvancadaRepository:IEducacaoTransporteEscolarRepository,IEducacaoTransporteDashboardService,IEducacaoTransporteRelatorioService,IEducacaoMerendaRepository,IEducacaoMerendaEstoqueService,IEducacaoMerendaCardapioService,IEducacaoMerendaRelatorioService,IEducacaoBibliotecaRepository,IEducacaoBibliotecaEmprestimoService,IEducacaoBibliotecaRelatorioService,IEducacaoIndicadoresRepository,IEducacaoFundebService,IEducacaoCustosService,IEducacaoEducacensoService,IEducacaoAvaliacaoMagisterioService
{
  private sealed record EducacaoTotais(long Total,long Ativos,long Pendentes,long Alertas);
  private sealed class EducacaoCardapioLinha{public long Id{get;set;} public long? EntidadeId{get;set;} public long? EscolaId{get;set;} public string? Status{get;set;} public DateOnly? Inicio{get;set;} public DateOnly? Fim{get;set;}}
 static readonly HashSet<string> Recursos=new(StringComparer.Ordinal){"educacao_transporte_rota","educacao_transporte_rota_ponto","educacao_transporte_veiculo","educacao_transporte_motorista","educacao_transporte_aluno","educacao_transporte_viagem","educacao_transporte_frequencia","educacao_transporte_ocorrencia","educacao_transporte_custo","educacao_merenda_produto","educacao_merenda_fornecedor","educacao_merenda_estoque","educacao_merenda_movimento","educacao_merenda_cardapio","educacao_merenda_distribuicao","educacao_merenda_requisicao","educacao_merenda_consumo","educacao_biblioteca_acervo","educacao_biblioteca_exemplar","educacao_biblioteca_emprestimo","educacao_biblioteca_reserva","educacao_biblioteca_ocorrencia","educacao_biblioteca_inventario","educacao_biblioteca_inventario_item","educacao_biblioteca_digital_referencia","educacao_fundeb_parametro","educacao_fundeb_receita","educacao_fundeb_aplicacao","educacao_custo_centro","educacao_custo_lancamento","educacao_custo_rateio","educacao_indicador","educacao_indicador_valor","educacao_projecao_matricula","educacao_educacenso_lote","educacao_educacenso_inconsistencia","educacao_avaliacao_magisterio","educacao_avaliacao_resultado"};
 readonly DapperContext _context; readonly IDatabaseObjectInspector _inspector; readonly IDateTimeProvider _clock;
  // Ciclo 2 Sprint 2 (BR-2.2): recursos com invariante tipada declarada — a criação genérica é
  // restrita e o comando tipado vira a ÚNICA porta de mutação (CQRS-lite). Recursos realmente
  // livres (fornecedor, centro de custo, requisição, consumo...) seguem genéricos. Residual
  // declarado: viagem/custo (transporte) permanecem genericos nesta RC; ocorrencia/inventario da
  // biblioteca ganharam comando tipado no Ciclo 2 S3 (BR-3.4/BR-3.5).
  // educacao_merenda_estoque não entra no mapa porque sua porta de criação é o método legado
  // RegistrarEstoqueAsync (PortaLegadaEstoqueAberta abaixo), não a CriarAsync genérica.
  static readonly Dictionary<string,string> RecursosComComandoTipado=new(StringComparer.Ordinal){
    ["educacao_merenda_movimento"]="POST /api/educacao/merenda/estoques/lancar",
    ["educacao_merenda_distribuicao"]="POST /api/educacao/merenda/distribuicoes/confirmar",
    ["educacao_biblioteca_emprestimo"]="POST /api/educacao/biblioteca/emprestimos/lancar",
    ["educacao_biblioteca_reserva"]="POST /api/educacao/biblioteca/reservas/lancar",
    ["educacao_indicador_valor"]="POST /api/educacao/indicadores/valores/lancar",
    ["educacao_fundeb_aplicacao"]="POST /api/educacao/fundeb/aplicacoes/lancar",
    ["educacao_biblioteca_ocorrencia"]="POST /api/educacao/biblioteca/exemplares/{id}/ocorrencia",
    ["educacao_biblioteca_inventario"]="POST /api/educacao/biblioteca/inventarios/lancar",
    ["educacao_biblioteca_inventario_item"]="POST /api/educacao/biblioteca/inventarios/{id}/itens/contar",
  };
  /// <summary>BR-2.2: porta legado de estoque (POST /estoques) restrita — a porta tipada é /estoques/lancar.
  /// Propriedade de runtime (não constante) para preservar o corpo legado documentado sem código inalcançável.</summary>
  public static bool PortaLegadaEstoqueAberta => false;
  public EducacaoAvancadaRepository(DapperContext context,IDatabaseObjectInspector inspector,IDateTimeProvider clock){_context=context;_inspector=inspector;_clock=clock;}
 public async Task<PagedResult<EducacaoAvancadaRegistroDto>> ListarAsync(long tenantId,string recurso,EducacaoAvancadaFiltro f,CancellationToken ct){recurso=Recurso(recurso);var p=Math.Max(1,f.Pagina);var t=Math.Clamp(f.Tamanho,1,100);if(!await _inspector.TableExistsAsync("sigov",recurso,ct).ConfigureAwait(false))throw new RecursoNaoDisponivelException(recurso);
    // C2S2 BR-2.6: 404 RECURSO_INDISPONIVEL uniforme em listar/criar/status/dashboard (era 200 + diagnóstico)
    var sql=$"select id as Id,codigo as Codigo,nome as Nome,status as Status,tipo as Tipo,descricao as Descricao,quantidade as Quantidade,valor as Valor,created_at as \"CreatedAt\",coalesce(updated_at,created_at) as \"UpdatedAt\" from sigov.{recurso} where tenant_id=@TenantId and is_deleted=false and (@EscolaId is null or escola_id=@EscolaId) and (@AnoLetivo is null or ano_letivo=@AnoLetivo) and (@Status is null or status=@Status) order by id desc limit @Tamanho offset @Offset;select count(1) from sigov.{recurso} where tenant_id=@TenantId and is_deleted=false and (@EscolaId is null or escola_id=@EscolaId) and (@AnoLetivo is null or ano_letivo=@AnoLetivo) and (@Status is null or status=@Status)";using var c=_context.CreateConnection();using var m=await c.QueryMultipleAsync(new CommandDefinition(sql,new{TenantId=tenantId,f.EscolaId,f.AnoLetivo,f.Status,Tamanho=t,Offset=(p-1)*t},cancellationToken:ct));var rows=(await m.ReadAsync<EducacaoAvancadaRegistroDto>()).ToList();return new(rows,p,t,await m.ReadSingleAsync<long>());}
 public async Task<long> CriarAsync(EducacaoAvancadaContext x,string recurso,EducacaoAvancadaOperacaoRequest r,CancellationToken ct){recurso=Recurso(recurso); // C2S2 BR-2.6+BR-2.2: schema ausente => 404 uniforme; recurso com invariante tipada => 409 COMANDO_TIPADO_OBRIGATORIO antes de qualquer efeito
    if(!await _inspector.TableExistsAsync("sigov",recurso,ct).ConfigureAwait(false))throw new RecursoNaoDisponivelException(recurso);
    if(RecursosComComandoTipado.TryGetValue(recurso,out var rotaTipada))throw new ComandoTipadoObrigatorioException(recurso,rotaTipada);
    Validar(recurso,r);await ValidarEscopoInstitucionalAsync(x.TenantId,x.EntidadeId,r.EscolaId,r.AnoLetivo,ct).ConfigureAwait(false);using var c=_context.CreateConnection();var sql=$"insert into sigov.{recurso}(tenant_id,entidade_id,exercicio_id,ano_letivo,escola_id,aluno_id,referencia_id,codigo,nome,tipo,status,descricao,justificativa,turno,quantidade,valor,dados,auditoria,correlation_id,created_by) values(@TenantId,@EntidadeId,@ExercicioId,@AnoLetivo,@EscolaId,@AlunoId,@ReferenciaId,@Codigo,@Nome,@Tipo,@Status,@Descricao,@Justificativa,@Turno,@Quantidade,@Valor,@Dados::jsonb,jsonb_build_array(jsonb_build_object('acao','CRIAR','em',now())),@CorrelationId,@UsuarioId) returning id";return await c.ExecuteScalarAsync<long>(new CommandDefinition(sql,new{x.TenantId,x.EntidadeId,x.ExercicioId,r.AnoLetivo,r.EscolaId,r.AlunoId,r.ReferenciaId,r.Codigo,r.Nome,r.Tipo,r.Status,r.Descricao,r.Justificativa,r.Turno,r.Quantidade,r.Valor,Dados=JsonSerializer.Serialize(r.Dados??new Dictionary<string,object?>()),CorrelationId=Guid.TryParse(x.CorrelationId,out var g)?g:Guid.NewGuid(),x.UsuarioId},cancellationToken:ct));}
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
 // Ciclo 2 Sprint 1 (merenda integridade transacional): registro legado de estoque preserva a
 // semântica de soma (sem linha cria; com linha acrescenta) agora transacional — linha de
 // saldo e movimento na MESMA transação, lock da linha baixada (for update), n==1 em toda
 // gravação e retry único quando a criação concorrente colide no backstop
 // ux_merenda_estoque_tenant_produto (23505 → refaz como acréscimo sobre a linha criada).
 // Produto inexistente/inativo falha nomeado (regra 13). Multi-esfera (regra 24).
 public async Task<long> RegistrarEstoqueAsync(EducacaoAvancadaContext x,EducacaoMerendaRegistrarEstoqueRequest r,CancellationToken ct){
   if(!await _inspector.TableExistsAsync("sigov","educacao_merenda_estoque",ct).ConfigureAwait(false))throw new RecursoNaoDisponivelException("educacao_merenda_estoque");
   if(!await _inspector.TableExistsAsync("sigov","educacao_merenda_movimento",ct).ConfigureAwait(false))throw new RecursoNaoDisponivelException("educacao_merenda_movimento");
   // C2S2 BR-2.2: porta legado de estoque restrita — o comando tipado POST /estoques/lancar e a unica porta de mutacao com invariante de inventario
    if(!PortaLegadaEstoqueAberta)throw new ComandoTipadoObrigatorioException("educacao_merenda_estoque","POST /api/educacao/merenda/estoques/lancar");
    if(r is null)throw new ArgumentException("Registro de estoque é obrigatório.");
   if(r.ProdutoId<=0)throw new ArgumentException("Registro de estoque exige produto.");
   var corr=Guid.TryParse(x.CorrelationId,out var g)?g:Guid.NewGuid();
   for(var tentativa=0;tentativa<2;tentativa++){
     using var c=(NpgsqlConnection)_context.CreateConnection();
     await c.OpenAsync(ct).ConfigureAwait(false);
     await using var tx=await c.BeginTransactionAsync(ct).ConfigureAwait(false);
     try{
       var ok=await c.ExecuteScalarAsync<bool>(new CommandDefinition("select exists(select 1 from sigov.educacao_merenda_produto where tenant_id=@TenantId and id=@Id and ativo and not is_deleted)",new{TenantId=x.TenantId,Id=r.ProdutoId},tx,cancellationToken:ct)).ConfigureAwait(false);
       if(!ok)throw new MerendaProdutoInexistenteException(r.ProdutoId);
       var est=await c.QueryFirstOrDefaultAsync<(long Id,decimal Saldo)>(new CommandDefinition("select id as Id,coalesce(saldo,0) as Saldo from sigov.educacao_merenda_estoque where tenant_id=@TenantId and produto_id=@Id and is_deleted=false order by id limit 1 for update",new{TenantId=x.TenantId,Id=r.ProdutoId},tx,cancellationToken:ct)).ConfigureAwait(false);
       long resultado;
       if(est.Id>0){
         var n=await c.ExecuteAsync(new CommandDefinition("update sigov.educacao_merenda_estoque set saldo=coalesce(saldo,0)+@Delta,updated_at=now(),updated_by=@UsuarioId where id=@Id",new{Delta=r.SaldoInicial,UsuarioId=x.UsuarioId,Id=est.Id},tx,cancellationToken:ct)).ConfigureAwait(false);
         if(n!=1)throw new ConflitoDeVersaoEducacaoException("educacao_merenda_estoque",est.Id);
         var nm=await GravarMovimentoEstoqueAsync(c,tx,x,r.ProdutoId,r.SaldoInicial>=0?"ENTRADA":"AJUSTE",r.SaldoInicial,corr,ct).ConfigureAwait(false);
         if(nm!=1)throw new ConflitoDeVersaoEducacaoException("educacao_merenda_movimento",r.ProdutoId);
         resultado=est.Id;
       }else{
         resultado=await c.ExecuteScalarAsync<long>(new CommandDefinition(@"insert into sigov.educacao_merenda_estoque(tenant_id,entidade_id,produto_id,nome,unidade,saldo,saldo_minimo,status,dados,auditoria,correlation_id,created_by)
 select @TenantId,@EntidadeId,p.id,p.nome,p.unidade,@SaldoInicial,coalesce(p.saldo_minimo,0),'ATIVA','{}'::jsonb,jsonb_build_array(jsonb_build_object('acao','REGISTRAR_ESTOQUE','em',now())),@CorrelationId,@UsuarioId
 from sigov.educacao_merenda_produto p where p.tenant_id=@TenantId and p.id=@Id and p.ativo and not p.is_deleted
 returning id",new{TenantId=x.TenantId,EntidadeId=x.EntidadeId,Id=r.ProdutoId,SaldoInicial=r.SaldoInicial,CorrelationId=corr,UsuarioId=x.UsuarioId},tx,cancellationToken:ct)).ConfigureAwait(false);
         if(resultado<=0)throw new ConflitoDeVersaoEducacaoException("educacao_merenda_estoque",r.ProdutoId);
         var nm=await GravarMovimentoEstoqueAsync(c,tx,x,r.ProdutoId,"ENTRADA",r.SaldoInicial,corr,ct).ConfigureAwait(false);
         if(nm!=1)throw new ConflitoDeVersaoEducacaoException("educacao_merenda_movimento",r.ProdutoId);
       }
       await tx.CommitAsync(ct).ConfigureAwait(false);
       return resultado;
     }catch(NpgsqlException pg) when(pg.SqlState is "23505"){
       await tx.RollbackAsync(ct).ConfigureAwait(false);
       if(tentativa==0)continue; // abertura concorrente ganhou a corrida — refaz somando sobre a linha criada
       throw new ConflitoDeVersaoEducacaoException("educacao_merenda_estoque",r.ProdutoId);
     }
   }
   throw new ConflitoDeVersaoEducacaoException("educacao_merenda_estoque",r.ProdutoId);
 }
 static Task<int> GravarMovimentoEstoqueAsync(NpgsqlConnection c,NpgsqlTransaction tx,EducacaoAvancadaContext x,long produtoId,string tipo,decimal qtd,Guid corr,CancellationToken ct)=>c.ExecuteAsync(new CommandDefinition("insert into sigov.educacao_merenda_movimento(tenant_id,entidade_id,produto_id,tipo,quantidade,competencia,status,dados,auditoria,correlation_id,created_by) values(@TenantId,@EntidadeId,@ProdutoId,@Tipo,@Qtd,current_date,'ATIVA','{}'::jsonb,jsonb_build_array(jsonb_build_object('acao','REGISTRAR_ESTOQUE','em',now())),@CorrelationId,@UsuarioId)",new{TenantId=x.TenantId,EntidadeId=x.EntidadeId,ProdutoId=produtoId,Tipo=tipo,Qtd=qtd,CorrelationId=corr,UsuarioId=x.UsuarioId},tx,cancellationToken:ct));
 // Ciclo 2 Sprint 1 (merenda integridade): lançamento tipado ABERTURA/ENTRADA/AJUSTE —
 // movimento e saldo gravados na MESMA transação com lock da linha; ABERTURA só sem linha
 // vigente (backstop ux_merenda_estoque_tenant_produto); AJUSTE exige justificativa e saldo
 // nunca fica negativo; ChaveIdempotencia torna o reenvio efeito único (retorna o id do
 // movimento original) e conteúdo divergente responde CHAVE_IDEMPOTENCIA_CONFLITO (regra 13).
 public async Task<long> LancarEstoqueAsync(EducacaoAvancadaContext x,EducacaoMerendaEstoqueLancarRequest r,CancellationToken ct){
   if(!await _inspector.TableExistsAsync("sigov","educacao_merenda_estoque",ct).ConfigureAwait(false))throw new RecursoNaoDisponivelException("educacao_merenda_estoque");
   if(!await _inspector.TableExistsAsync("sigov","educacao_merenda_movimento",ct).ConfigureAwait(false))throw new RecursoNaoDisponivelException("educacao_merenda_movimento");
   if(r is null)throw new ArgumentException("Lançamento de estoque é obrigatório.");
   if(r.ProdutoId<=0)throw new ArgumentException("Lançamento de estoque exige produto.");
   var op=r.Operacao?.Trim().ToUpperInvariant();
   if(op is not ("ABERTURA" or "ENTRADA" or "AJUSTE"))throw new EstoqueOperacaoInvalidaException(r.Operacao ?? "VAZIA");
   if(op!="AJUSTE"&&r.Quantidade<=0)throw new ArgumentException("Quantidade deve ser maior que zero para ABERTURA e ENTRADA.");
   if(op=="AJUSTE"&&r.Quantidade==0)throw new ArgumentException("Ajuste exige quantidade diferente de zero.");
   if(op=="AJUSTE"&&string.IsNullOrWhiteSpace(r.Motivo))throw new EstornoMotivoObrigatorioException();
   var chave=string.IsNullOrWhiteSpace(r.ChaveIdempotencia)?null:r.ChaveIdempotencia.Trim();
   var corr=Guid.TryParse(x.CorrelationId,out var g)?g:Guid.NewGuid();
   using var c=(NpgsqlConnection)_context.CreateConnection();
   await c.OpenAsync(ct).ConfigureAwait(false);
   await using var tx=await c.BeginTransactionAsync(ct).ConfigureAwait(false);
   var ok=await c.ExecuteScalarAsync<bool>(new CommandDefinition("select exists(select 1 from sigov.educacao_merenda_produto where tenant_id=@TenantId and id=@Id and ativo and not is_deleted)",new{TenantId=x.TenantId,Id=r.ProdutoId},tx,cancellationToken:ct)).ConfigureAwait(false);
   if(!ok)throw new MerendaProdutoInexistenteException(r.ProdutoId);
   var tipoMov=op=="ABERTURA"?"ENTRADA":op;
   if(chave is not null){
     var anterior=await c.QueryFirstOrDefaultAsync<(long Id,long Produto,string Tipo,decimal Qtd)>(new CommandDefinition("select m.id as Id,m.produto_id as Produto,m.tipo as Tipo,coalesce(m.quantidade,0) as Qtd from sigov.educacao_merenda_movimento m where m.tenant_id=@TenantId and m.chave_idempotencia=@Chave and m.is_deleted=false order by m.id limit 1",new{TenantId=x.TenantId,Chave=chave},tx,cancellationToken:ct)).ConfigureAwait(false);
     if(anterior.Id>0){
       if(anterior.Produto!=r.ProdutoId||anterior.Tipo!=tipoMov||anterior.Qtd!=r.Quantidade)throw new ChaveIdempotenciaConflitoException(chave);
       return anterior.Id; // reenvio → efeito único
     }
   }
   var est=await c.QueryFirstOrDefaultAsync<(long Id,decimal Saldo)>(new CommandDefinition("select id as Id,coalesce(saldo,0) as Saldo from sigov.educacao_merenda_estoque where tenant_id=@TenantId and produto_id=@Id and is_deleted=false order by id limit 1 for update",new{TenantId=x.TenantId,Id=r.ProdutoId},tx,cancellationToken:ct)).ConfigureAwait(false);
   long estoqueId;
   if(op=="ABERTURA"){
     if(est.Id>0)throw new EstoqueAberturaExisteException(r.ProdutoId);
     try{
       estoqueId=await c.ExecuteScalarAsync<long>(new CommandDefinition(@"insert into sigov.educacao_merenda_estoque(tenant_id,entidade_id,produto_id,nome,unidade,saldo,saldo_minimo,status,dados,auditoria,correlation_id,created_by)
 select @TenantId,@EntidadeId,p.id,p.nome,p.unidade,@Qtd,coalesce(p.saldo_minimo,0),'ATIVA','{}'::jsonb,jsonb_build_array(jsonb_build_object('acao','ESTOQUE_ABERTURA','em',now())),@CorrelationId,@UsuarioId
 from sigov.educacao_merenda_produto p where p.tenant_id=@TenantId and p.id=@Id and p.ativo and not p.is_deleted
 returning id",new{TenantId=x.TenantId,EntidadeId=x.EntidadeId,Id=r.ProdutoId,Qtd=r.Quantidade,CorrelationId=corr,UsuarioId=x.UsuarioId},tx,cancellationToken:ct)).ConfigureAwait(false);
       if(estoqueId<=0)throw new ConflitoDeVersaoEducacaoException("educacao_merenda_estoque",r.ProdutoId);
     }catch(NpgsqlException pg) when(pg.SqlState is "23505"){
       throw new EstoqueAberturaExisteException(r.ProdutoId); // abertura concorrente — unicidade garantida pelo índice
     }
   }else{
     if(est.Id==0)throw new EstoqueNaoAbertoException(r.ProdutoId);
     var novoSaldo=est.Saldo+r.Quantidade;
     if(novoSaldo<0m)throw new SaldoNegativoRejeitadoException(r.ProdutoId,novoSaldo);
     var n=await c.ExecuteAsync(new CommandDefinition("update sigov.educacao_merenda_estoque set saldo=coalesce(saldo,0)+@Delta,updated_at=now(),updated_by=@UsuarioId where id=@Id",new{Delta=r.Quantidade,UsuarioId=x.UsuarioId,Id=est.Id},tx,cancellationToken:ct)).ConfigureAwait(false);
     if(n!=1)throw new ConflitoDeVersaoEducacaoException("educacao_merenda_estoque",est.Id);
     estoqueId=est.Id;
   }
   var dados=JsonSerializer.Serialize(new Dictionary<string,object?>{{"operacao",op},{"lote",r.Lote},{"validade",r.Validade?.ToString("yyyy-MM-dd")},{"origem",r.Origem},{"motivo",op=="AJUSTE"?r.Motivo?.Trim():null}});
   var movId=await c.ExecuteScalarAsync<long>(new CommandDefinition("insert into sigov.educacao_merenda_movimento(tenant_id,entidade_id,referencia_id,produto_id,tipo,quantidade,competencia,status,dados,auditoria,correlation_id,created_by,chave_idempotencia) values(@TenantId,@EntidadeId,@EstoqueId,@ProdutoId,@Tipo,@Qtd,current_date,'ATIVA',@Dados::jsonb,jsonb_build_array(jsonb_build_object('acao','LANCAR_ESTOQUE','operacao',@Op,'em',now())),@CorrelationId,@UsuarioId,@Chave) returning id",new{TenantId=x.TenantId,EntidadeId=x.EntidadeId,EstoqueId=estoqueId,ProdutoId=r.ProdutoId,Tipo=tipoMov,Qtd=r.Quantidade,Dados=dados,Op=op,CorrelationId=corr,UsuarioId=x.UsuarioId,Chave=chave},tx,cancellationToken:ct)).ConfigureAwait(false);
   await tx.CommitAsync(ct).ConfigureAwait(false);
   return movId;
 }
 // Ciclo 2 Sprint 1 (merenda integridade): distribuição de refeição com revalidação DENTRO da
 // transação — cardápio travado com for update (status/período/escopo), dia livre com
 // reenvio idempotente por chaveIdempotencia (mesma chave + mesmo conteúdo retorna o id
 // original sem nova baixa; conteúdo divergente → CHAVE_IDEMPOTENCIA_CONFLITO; dia ocupado →
 // DISTRIBUICAO_DIA_EXISTENTE), lock e baixa na MESMA linha de saldo (única por tenant ×
 // produto) com n==1 verificado em itens e movimentos. Backstop no índice
 // ux_merenda_distribuicao_tenant_cardapio_data. Falhas nomeadas (regra 13); multi-esfera (regra 24).
 public async Task<long> DistribuirRefeicaoAsync(EducacaoAvancadaContext x,EducacaoMerendaDistribuirRefeicaoRequest r,CancellationToken ct){
   if(!await _inspector.TableExistsAsync("sigov","educacao_merenda_distribuicao",ct).ConfigureAwait(false))throw new RecursoNaoDisponivelException("educacao_merenda_distribuicao");
   if(!await _inspector.TableExistsAsync("sigov","educacao_merenda_distribuicao_item",ct).ConfigureAwait(false))throw new RecursoNaoDisponivelException("educacao_merenda_distribuicao_item");
   if(!await _inspector.TableExistsAsync("sigov","educacao_merenda_estoque",ct).ConfigureAwait(false))throw new RecursoNaoDisponivelException("educacao_merenda_estoque");
   if(!await _inspector.TableExistsAsync("sigov","educacao_merenda_movimento",ct).ConfigureAwait(false))throw new RecursoNaoDisponivelException("educacao_merenda_movimento");
   if(r is null)throw new ArgumentException("Distribuição é obrigatória.");
   if(r.CardapioId<=0)throw new ArgumentException("Distribuição exige cardápio.");
   if(r.Itens is null||r.Itens.Count==0)throw new ArgumentException("Distribuição exige ao menos um item.");
   var itens=new List<EducacaoMerendaDistribuirItemRequest>();
   var vistos=new HashSet<long>();
   foreach(var it in r.Itens){if(it is null||it.ProdutoId<=0)throw new ArgumentException("Item inválido na distribuição.");if(it.Quantidade<=0)throw new ArgumentException("Quantidade do item deve ser maior que zero.");if(!vistos.Add(it.ProdutoId))throw new ArgumentException("Produto repetido na distribuição.");itens.Add(it);}
   var chave=string.IsNullOrWhiteSpace(r.ChaveIdempotencia)?null:r.ChaveIdempotencia.Trim();
   var corr=Guid.TryParse(x.CorrelationId,out var g2)?g2:Guid.NewGuid();
   using var c=(NpgsqlConnection)_context.CreateConnection();
   await c.OpenAsync(ct).ConfigureAwait(false);
   await using var tx=await c.BeginTransactionAsync(ct).ConfigureAwait(false);
   var card=await c.QueryFirstOrDefaultAsync<EducacaoCardapioLinha>(new CommandDefinition("select id as Id,entidade_id as EntidadeId,escola_id as EscolaId,status as Status,data_inicio as Inicio,data_fim as Fim from sigov.educacao_merenda_cardapio where tenant_id=@TenantId and id=@Id and is_deleted=false for update",new{TenantId=x.TenantId,Id=r.CardapioId},tx,cancellationToken:ct)).ConfigureAwait(false);
   if(card is null||card.Id==0)throw new DistribuicaoCardapioInexistenteException(r.CardapioId);
   if(x.EntidadeId.HasValue&&card.EntidadeId.HasValue&&card.EntidadeId.Value!=x.EntidadeId.Value)throw new CardapioForaDoEscopoException(r.CardapioId,card.EntidadeId.Value,x.EntidadeId.Value);
   if(card.Status is not ("ATIVA" or "ATIVO"))throw new CardapioNaoAprovadoException(r.CardapioId,card.Status ?? "INDEFINIDO");
   var ini=card.Inicio;var fim=card.Fim;
   if(ini==null||fim==null)throw new DataForaDoPeriodoException(r.CardapioId,r.Data,r.Data,r.Data);
   if(r.Data<ini||r.Data>fim)throw new DataForaDoPeriodoException(r.CardapioId,r.Data,ini.Value,fim.Value);
   var noMenu=await c.ExecuteScalarAsync<int>(new CommandDefinition("select count(distinct u.p) from unnest(@Ids) as u(p) where exists(select 1 from sigov.educacao_merenda_cardapio_item i where i.referencia_id=@CardapioId and i.produto_id=u.p and i.is_deleted=false)",new{Ids=itens.Select(i=>i.ProdutoId).ToArray(),CardapioId=r.CardapioId},tx,cancellationToken:ct)).ConfigureAwait(false);
   if(noMenu!=itens.Count){
     long fora=0;
     foreach(var it in itens){
       var m=await c.ExecuteScalarAsync<bool>(new CommandDefinition("select exists(select 1 from sigov.educacao_merenda_cardapio_item i where i.referencia_id=@CardapioId and i.produto_id=@P and i.is_deleted=false)",new{CardapioId=r.CardapioId,P=it.ProdutoId},tx,cancellationToken:ct)).ConfigureAwait(false);
       if(!m){fora=it.ProdutoId;break;}
     }
     throw new ProdutoForaDoMenuException(fora,r.CardapioId);
   }
   if(chave is not null){
     var porChave=await c.QueryFirstOrDefaultAsync<(long Id,long Cardapio,DateOnly Data,string Status)>(new CommandDefinition("select d.id as Id,d.referencia_id as Cardapio,d.competencia as Data,d.status as Status from sigov.educacao_merenda_distribuicao d where d.tenant_id=@TenantId and d.dados->>'chaveIdempotencia'=@Chave and d.is_deleted=false order by d.id desc limit 1",new{TenantId=x.TenantId,Chave=chave},tx,cancellationToken:ct)).ConfigureAwait(false);
     if(porChave.Id>0&&porChave.Status is not ("CANCELADA" or "CANCELADO" or "ENCERRADA" or "ENCERRADO")){
       if(porChave.Cardapio==r.CardapioId&&porChave.Data==r.Data&&await MesmosItensDistribuicaoAsync(c,tx,porChave.Id,itens,ct).ConfigureAwait(false))return porChave.Id;
       throw new ChaveIdempotenciaConflitoException(chave);
     }
   }
   var ocupante=await c.QueryFirstOrDefaultAsync<(long Id,string Chave)>(new CommandDefinition("select d.id as Id,coalesce(d.dados->>'chaveIdempotencia','') as Chave from sigov.educacao_merenda_distribuicao d where d.tenant_id=@TenantId and d.referencia_id=@CardapioId and d.competencia=@Data and d.is_deleted=false and d.status not in ('ENCERRADA','ENCERRADO','CANCELADA','CANCELADO') for update",new{TenantId=x.TenantId,CardapioId=r.CardapioId,Data=r.Data},tx,cancellationToken:ct)).ConfigureAwait(false);
   if(ocupante.Id>0){
     if(chave is not null&&string.Equals(ocupante.Chave,chave,StringComparison.Ordinal)&&await MesmosItensDistribuicaoAsync(c,tx,ocupante.Id,itens,ct).ConfigureAwait(false))return ocupante.Id;
     throw new DistribuicaoDiaExistenteException(r.CardapioId,r.Data);
   }
   var travados=new Dictionary<long,(long Id,decimal Saldo)>();
   foreach(var it in itens.OrderBy(i=>i.ProdutoId)){
     var est=await c.QueryFirstOrDefaultAsync<(long Id,decimal Saldo)>(new CommandDefinition("select id as Id,coalesce(saldo,0) as Saldo from sigov.educacao_merenda_estoque where tenant_id=@TenantId and produto_id=@P and is_deleted=false order by id limit 1 for update",new{TenantId=x.TenantId,P=it.ProdutoId},tx,cancellationToken:ct)).ConfigureAwait(false);
     var disponivel=est.Id>0?est.Saldo:0m;
     if(disponivel<it.Quantidade)throw new EstoqueInsuficienteException(it.ProdutoId,disponivel,it.Quantidade);
     if(est.Id>0)travados[it.ProdutoId]=est;
   }
   long did;
   try{
     did=await c.ExecuteScalarAsync<long>(new CommandDefinition("insert into sigov.educacao_merenda_distribuicao(tenant_id,entidade_id,escola_id,referencia_id,competencia,tipo,status,dados,auditoria,correlation_id,created_by) values(@TenantId,@EntidadeId,@EscolaId,@CardapioId,@Data,'REFEICAO','ATIVA',@Dados::jsonb,jsonb_build_array(jsonb_build_object('acao','DISTRIBUIR_REFEICAO','em',now())),@CorrelationId,@UsuarioId) returning id",new{TenantId=x.TenantId,EntidadeId=x.EntidadeId,EscolaId=card.EscolaId,CardapioId=r.CardapioId,Data=r.Data,Dados=JsonSerializer.Serialize(chave is null?new Dictionary<string,object?>():new Dictionary<string,object?>{{"chaveIdempotencia",chave}}),CorrelationId=corr,UsuarioId=x.UsuarioId},tx,cancellationToken:ct)).ConfigureAwait(false);
   }catch(NpgsqlException pg) when(pg.SqlState is "23505"){
     throw new DistribuicaoDiaExistenteException(r.CardapioId,r.Data); // inserção concorrente ganhou o dia
   }
   foreach(var it in itens){
     var nItem=await c.ExecuteAsync(new CommandDefinition("insert into sigov.educacao_merenda_distribuicao_item(tenant_id,entidade_id,escola_id,referencia_id,produto_id,nome,unidade,quantidade,competencia,status,dados,auditoria,correlation_id,created_by) select @TenantId,@EntidadeId,@EscolaId,@DistribuidaoId,p.id,p.nome,p.unidade,@Qtd,@Data,'ATIVA','{}'::jsonb,jsonb_build_array(jsonb_build_object('acao','DISTRIBUIR_REFEICAO','em',now())),@CorrelationId,@UsuarioId from sigov.educacao_merenda_produto p where p.tenant_id=@TenantId and p.id=@Id and p.ativo and not p.is_deleted",new{TenantId=x.TenantId,EntidadeId=x.EntidadeId,EscolaId=card.EscolaId,DistribuidaoId=did,Id=it.ProdutoId,Qtd=it.Quantidade,Data=r.Data,CorrelationId=corr,UsuarioId=x.UsuarioId},tx,cancellationToken:ct)).ConfigureAwait(false);
     if(nItem!=1)throw new MerendaProdutoInexistenteException(it.ProdutoId);
     var nMov=await c.ExecuteAsync(new CommandDefinition("insert into sigov.educacao_merenda_movimento(tenant_id,entidade_id,referencia_id,produto_id,tipo,quantidade,competencia,status,dados,auditoria,correlation_id,created_by) values(@TenantId,@EntidadeId,@DistribuidaoId,@Id,'SAIDA',-@Qtd,@Data,'ATIVA','{}'::jsonb,jsonb_build_array(jsonb_build_object('acao','BAIXA_DISTRIBUICAO','em',now())),@CorrelationId,@UsuarioId)",new{TenantId=x.TenantId,EntidadeId=x.EntidadeId,DistribuidaoId=did,Id=it.ProdutoId,Qtd=it.Quantidade,Data=r.Data,CorrelationId=corr,UsuarioId=x.UsuarioId},tx,cancellationToken:ct)).ConfigureAwait(false);
     if(nMov!=1)throw new ConflitoDeVersaoEducacaoException("educacao_merenda_movimento",did);
     if(travados.TryGetValue(it.ProdutoId,out var travado)){
       var nBaixa=await c.ExecuteAsync(new CommandDefinition("update sigov.educacao_merenda_estoque set saldo=coalesce(saldo,0)-@Qtd,updated_at=now() where id=@Id",new{Qtd=it.Quantidade,Id=travado.Id},tx,cancellationToken:ct)).ConfigureAwait(false);
       if(nBaixa!=1)throw new ConflitoDeVersaoEducacaoException("educacao_merenda_estoque",travado.Id);
     }
   }
   await tx.CommitAsync(ct).ConfigureAwait(false);
   return did;
 }
 static async Task<bool> MesmosItensDistribuicaoAsync(NpgsqlConnection c,NpgsqlTransaction tx,long distribuicaoId,IReadOnlyCollection<EducacaoMerendaDistribuirItemRequest> itens,CancellationToken ct){
   var atuais=(await c.QueryAsync<(long P,decimal Q)>(new CommandDefinition("select produto_id as P,coalesce(quantidade,0) as Q from sigov.educacao_merenda_distribuicao_item where referencia_id=@Id and is_deleted=false order by produto_id",new{Id=distribuicaoId},tx,cancellationToken:ct)).ConfigureAwait(false)).ToList();
   if(atuais.Count!=itens.Count)return false;
   foreach(var it in itens.OrderBy(i=>i.ProdutoId)){
     if(!atuais.Contains((it.ProdutoId,it.Quantidade)))return false;
   }
   return true;
 }
 // Ciclo 2 Sprint 1 (merenda integridade): estorno de movimento de estoque — compensação única
 // por base (backstop ux_merenda_movimento_tenant_estorno_ref), justificativa obrigatória, saldo
 // nunca negativo; reenvio retorna o id do estorno já registrado (efeito único). Movimento de
 // saída de distribuição já compensado pelo estorno da distribuição não pode ser estornado
 // individualmente (compensação dupla). Falhas nomeadas (regra 13); multi-esfera (regra 24).
 public async Task<long> EstornarMovimentoAsync(EducacaoAvancadaContext x,long movimentoId,EducacaoMerendaEstornoRequest r,CancellationToken ct){
   if(!await _inspector.TableExistsAsync("sigov","educacao_merenda_movimento",ct).ConfigureAwait(false))throw new RecursoNaoDisponivelException("educacao_merenda_movimento");
   if(!await _inspector.TableExistsAsync("sigov","educacao_merenda_estoque",ct).ConfigureAwait(false))throw new RecursoNaoDisponivelException("educacao_merenda_estoque");
   if(r is null||string.IsNullOrWhiteSpace(r.Motivo))throw new EstornoMotivoObrigatorioException();
   var corr=Guid.TryParse(x.CorrelationId,out var g)?g:Guid.NewGuid();
   using var c=(NpgsqlConnection)_context.CreateConnection();
   await c.OpenAsync(ct).ConfigureAwait(false);
   await using var tx=await c.BeginTransactionAsync(ct).ConfigureAwait(false);
   var baseMov=await c.QueryFirstOrDefaultAsync<(long Id,long Entidade,long Produto,long Referencia,string Tipo,decimal Qtd)>(new CommandDefinition("select m.id as Id,coalesce(m.entidade_id,-1) as Entidade,m.produto_id as Produto,coalesce(m.referencia_id,0) as Referencia,m.tipo as Tipo,coalesce(m.quantidade,0) as Qtd from sigov.educacao_merenda_movimento m where m.tenant_id=@TenantId and m.id=@Id and m.is_deleted=false",new{TenantId=x.TenantId,Id=movimentoId},tx,cancellationToken:ct)).ConfigureAwait(false);
   if(baseMov.Id==0)throw new EstornoBaseInexistenteException(movimentoId);
   if(baseMov.Tipo is not ("ENTRADA" or "SAIDA" or "AJUSTE"))throw new EstornoBaseInvalidaException(movimentoId,baseMov.Tipo ?? "VAZIO");
   if(baseMov.Entidade>=0&&x.EntidadeId.HasValue&&baseMov.Entidade!=x.EntidadeId.Value)throw new ContextoInstitucionalDivergenteException("educacao_merenda_movimento",movimentoId,baseMov.Entidade,x.EntidadeId.Value);
   if(baseMov.Referencia>0&&baseMov.Tipo=="SAIDA"){
     var compensadaPelaDistribuicao=await c.ExecuteScalarAsync<bool>(new CommandDefinition("select exists(select 1 from sigov.educacao_merenda_movimento e where e.tenant_id=@TenantId and e.referencia_id=@Ref and e.produto_id=@Produto and e.tipo='ESTORNO' and e.is_deleted=false)",new{TenantId=x.TenantId,Ref=baseMov.Referencia,Produto=baseMov.Produto},tx,cancellationToken:ct)).ConfigureAwait(false);
     if(compensadaPelaDistribuicao)throw new EstornoBaseInvalidaException(movimentoId,"SAIDA_JA_COMPENSADA_POR_ESTORNO_DE_DISTRIBUICAO");
   }
   var existente=await c.ExecuteScalarAsync<long>(new CommandDefinition("select coalesce(min(id),0) from sigov.educacao_merenda_movimento where tenant_id=@TenantId and referencia_id=@BaseId and produto_id=@Produto and tipo='ESTORNO' and is_deleted=false",new{TenantId=x.TenantId,BaseId=movimentoId,Produto=baseMov.Produto},tx,cancellationToken:ct)).ConfigureAwait(false);
   if(existente>0)return existente; // reenvio → efeito único
   var est=await c.QueryFirstOrDefaultAsync<(long Id,decimal Saldo)>(new CommandDefinition("select id as Id,coalesce(saldo,0) as Saldo from sigov.educacao_merenda_estoque where tenant_id=@TenantId and produto_id=@Id and is_deleted=false order by id limit 1 for update",new{TenantId=x.TenantId,Id=baseMov.Produto},tx,cancellationToken:ct)).ConfigureAwait(false);
   if(est.Id==0)throw new EstoqueNaoAbertoException(baseMov.Produto);
   var delta=-baseMov.Qtd;
   var novoSaldo=est.Saldo+delta;
   if(novoSaldo<0m)throw new SaldoNegativoRejeitadoException(baseMov.Produto,novoSaldo);
   var n=await c.ExecuteAsync(new CommandDefinition("update sigov.educacao_merenda_estoque set saldo=coalesce(saldo,0)+@Delta,updated_at=now(),updated_by=@UsuarioId where id=@Id",new{Delta=delta,UsuarioId=x.UsuarioId,Id=est.Id},tx,cancellationToken:ct)).ConfigureAwait(false);
   if(n!=1)throw new ConflitoDeVersaoEducacaoException("educacao_merenda_estoque",est.Id);
   var estornoId=await c.ExecuteScalarAsync<long>(new CommandDefinition("insert into sigov.educacao_merenda_movimento(tenant_id,entidade_id,referencia_id,produto_id,tipo,quantidade,competencia,justificativa,status,dados,auditoria,correlation_id,created_by) values(@TenantId,@EntidadeId,@BaseId,@Produto,'ESTORNO',@Qtd,current_date,@Motivo,'ATIVA','{}'::jsonb,jsonb_build_array(jsonb_build_object('acao','ESTORNAR_MOVIMENTO','base',@BaseId,'motivo',@Motivo,'em',now())),@CorrelationId,@UsuarioId) returning id",new{TenantId=x.TenantId,EntidadeId=x.EntidadeId,BaseId=movimentoId,Produto=baseMov.Produto,Qtd=delta,Motivo=r.Motivo.Trim(),CorrelationId=corr,UsuarioId=x.UsuarioId},tx,cancellationToken:ct)).ConfigureAwait(false);
   await tx.CommitAsync(ct).ConfigureAwait(false);
   return estornoId;
 }
 // Ciclo 2 Sprint 1 (merenda integridade): estorno de distribuição — restaura o saldo de cada
 // item consumido (lock da linha + n==1), grava um movimento ESTORNO por item com justificativa
 // e marca a distribuição CANCELADA preservando documento, itens e auditoria (append-only);
 // reenvio tem efeito único (retorna o id da distribuição); itens já compensados via estorno de
 // movimento não são compensados de novo. Falhas nomeadas (regra 13); multi-esfera (regra 24).
 public async Task<long> EstornarDistribuicaoAsync(EducacaoAvancadaContext x,long distribuicaoId,EducacaoMerendaEstornoRequest r,CancellationToken ct){
   if(!await _inspector.TableExistsAsync("sigov","educacao_merenda_distribuicao",ct).ConfigureAwait(false))throw new RecursoNaoDisponivelException("educacao_merenda_distribuicao");
   if(!await _inspector.TableExistsAsync("sigov","educacao_merenda_movimento",ct).ConfigureAwait(false))throw new RecursoNaoDisponivelException("educacao_merenda_movimento");
   if(!await _inspector.TableExistsAsync("sigov","educacao_merenda_estoque",ct).ConfigureAwait(false))throw new RecursoNaoDisponivelException("educacao_merenda_estoque");
   if(r is null||string.IsNullOrWhiteSpace(r.Motivo))throw new EstornoMotivoObrigatorioException();
   var corr=Guid.TryParse(x.CorrelationId,out var g)?g:Guid.NewGuid();
   using var c=(NpgsqlConnection)_context.CreateConnection();
   await c.OpenAsync(ct).ConfigureAwait(false);
   await using var tx=await c.BeginTransactionAsync(ct).ConfigureAwait(false);
   var dist=await c.QueryFirstOrDefaultAsync<(long Id,long Entidade,string Status)>(new CommandDefinition("select d.id as Id,coalesce(d.entidade_id,-1) as Entidade,d.status as Status from sigov.educacao_merenda_distribuicao d where d.tenant_id=@TenantId and d.id=@Id and d.is_deleted=false for update",new{TenantId=x.TenantId,Id=distribuicaoId},tx,cancellationToken:ct)).ConfigureAwait(false);
   if(dist.Id==0)throw new RegistroNaoEncontradoException("educacao_merenda_distribuicao",distribuicaoId);
   if(dist.Entidade>=0&&x.EntidadeId.HasValue&&dist.Entidade!=x.EntidadeId.Value)throw new ContextoInstitucionalDivergenteException("educacao_merenda_distribuicao",distribuicaoId,dist.Entidade,x.EntidadeId.Value);
   var compensacoesVinculadas=await c.ExecuteScalarAsync<long>(new CommandDefinition("select coalesce(count(1),0) from sigov.educacao_merenda_movimento where tenant_id=@TenantId and referencia_id=@Id and tipo='ESTORNO' and is_deleted=false",new{TenantId=x.TenantId,Id=distribuicaoId},tx,cancellationToken:ct)).ConfigureAwait(false);
   if(compensacoesVinculadas>0)return distribuicaoId; // reenvio → efeito único
   var bases=(await c.QueryAsync<(long Produto,long MovId,decimal Qtd)>(new CommandDefinition("select m.produto_id as Produto,m.id as MovId,coalesce(m.quantidade,0) as Qtd from sigov.educacao_merenda_movimento m where m.tenant_id=@TenantId and m.referencia_id=@Id and m.tipo='SAIDA' and m.is_deleted=false order by m.produto_id",new{TenantId=x.TenantId,Id=distribuicaoId},tx,cancellationToken:ct)).ConfigureAwait(false)).ToList();
   var efeitosCompensados=0;
   foreach(var b in bases){
     var jaCompensado=await c.ExecuteScalarAsync<bool>(new CommandDefinition("select exists(select 1 from sigov.educacao_merenda_movimento e where e.tenant_id=@TenantId and e.referencia_id=@MovId and e.tipo='ESTORNO' and e.is_deleted=false)",new{TenantId=x.TenantId,MovId=b.MovId},tx,cancellationToken:ct)).ConfigureAwait(false);
     if(jaCompensado)continue;
     var est=await c.QueryFirstOrDefaultAsync<(long Id,decimal Saldo)>(new CommandDefinition("select id as Id,coalesce(saldo,0) as Saldo from sigov.educacao_merenda_estoque where tenant_id=@TenantId and produto_id=@Id and is_deleted=false order by id limit 1 for update",new{TenantId=x.TenantId,Id=b.Produto},tx,cancellationToken:ct)).ConfigureAwait(false);
     if(est.Id==0)throw new EstoqueNaoAbertoException(b.Produto);
     var delta=-b.Qtd;
     var novoSaldo=est.Saldo+delta;
     if(novoSaldo<0m)throw new SaldoNegativoRejeitadoException(b.Produto,novoSaldo);
     var n=await c.ExecuteAsync(new CommandDefinition("update sigov.educacao_merenda_estoque set saldo=coalesce(saldo,0)+@Delta,updated_at=now(),updated_by=@UsuarioId where id=@Id",new{Delta=delta,UsuarioId=x.UsuarioId,Id=est.Id},tx,cancellationToken:ct)).ConfigureAwait(false);
     if(n!=1)throw new ConflitoDeVersaoEducacaoException("educacao_merenda_estoque",est.Id);
     var nm=await c.ExecuteAsync(new CommandDefinition("insert into sigov.educacao_merenda_movimento(tenant_id,entidade_id,referencia_id,produto_id,tipo,quantidade,competencia,justificativa,status,dados,auditoria,correlation_id,created_by) values(@TenantId,@EntidadeId,@DistribuidaoId,@Produto,'ESTORNO',@Qtd,current_date,@Motivo,'ATIVA','{}'::jsonb,jsonb_build_array(jsonb_build_object('acao','ESTORNAR_DISTRIBUICAO','base_movimento',@MovId,'motivo',@Motivo,'em',now())),@CorrelationId,@UsuarioId)",new{TenantId=x.TenantId,EntidadeId=x.EntidadeId,DistribuidaoId=distribuicaoId,Produto=b.Produto,Qtd=delta,Motivo=r.Motivo.Trim(),MovId=b.MovId,CorrelationId=corr,UsuarioId=x.UsuarioId},tx,cancellationToken:ct)).ConfigureAwait(false);
     if(nm!=1)throw new ConflitoDeVersaoEducacaoException("educacao_merenda_movimento",distribuicaoId);
     efeitosCompensados++;
   }
   var ativa=dist.Status is ("ATIVA" or "ATIVO");
   if(!ativa&&efeitosCompensados==0&&bases.Count==0)throw new DistribuicaoEstornoIndisponivelException(distribuicaoId,dist.Status ?? "INDEFINIDO");
   var nu=await c.ExecuteAsync(new CommandDefinition("update sigov.educacao_merenda_distribuicao set status='CANCELADA',justificativa=@Justificativa,updated_at=now(),updated_by=@UsuarioId,correlation_id=@CorrelationId,auditoria=(case jsonb_typeof(auditoria) when 'array' then auditoria when 'object' then case when auditoria='{}'::jsonb then '[]'::jsonb else jsonb_build_array(auditoria) end else '[]'::jsonb end)||jsonb_build_array(jsonb_build_object('acao','ESTORNAR_DISTRIBUICAO','motivo',@Justificativa,'em',now())) where tenant_id=@TenantId and id=@Id and is_deleted=false",new{Justificativa=r.Motivo.Trim(),UsuarioId=x.UsuarioId,CorrelationId=corr,TenantId=x.TenantId,Id=distribuicaoId},tx,cancellationToken:ct)).ConfigureAwait(false);
   if(nu!=1)throw new ConflitoDeVersaoEducacaoException("educacao_merenda_distribuicao",distribuicaoId);
   await tx.CommitAsync(ct).ConfigureAwait(false);
   return distribuicaoId;
 }
// Sprint 7 (biblioteca circulação): empréstimo, devolução e reserva com
// unicidade de vigência por exemplar (backstop nos índices parciais da
// migration 20261011160000). Tomador = exatamente um de aluno/servidor do
// tenant (regra 24: válido para qualquer esfera/órgão). Falhas nomeadas (regra 13).
 private sealed class EducacaoExemplarLinha{public long Id{get;set;} public long? EntidadeId{get;set;} public long? EscolaId{get;set;} public string? Status{get;set;}}
 private sealed class EducacaoEmprestimoLinha{public long Id{get;set;} public long? EntidadeId{get;set;} public long? EscolaId{get;set;} public long? ExemplarId{get;set;} public long? AlunoId{get;set;} public long? ServidorId{get;set;} public string? Status{get;set;} public DateOnly? DataFim{get;set;} public DateOnly? PrazoOriginal{get;set;} public int Renovacoes{get;set;}}
 static void ValidarTomador(long? AlunoId,long? ServidorId){
   var temAluno=AlunoId.HasValue&&AlunoId.Value>0;
   var temServidor=ServidorId.HasValue&&ServidorId.Value>0;
   if(temAluno==temServidor)throw new EmprestimoTomadorInvalidoException(AlunoId,ServidorId);
 }
 async Task<EducacaoExemplarLinha> CarregarExemplarNoEscopoAsync(EducacaoAvancadaContext x,long exemplarId,CancellationToken ct,NpgsqlConnection c){
   var ex=await c.QueryFirstOrDefaultAsync<EducacaoExemplarLinha>(new CommandDefinition("select id as Id,entidade_id as EntidadeId,escola_id as EscolaId,status as Status from sigov.educacao_biblioteca_exemplar where tenant_id=@TenantId and id=@Id and ativo and not is_deleted",new{TenantId=x.TenantId,Id=exemplarId},cancellationToken:ct)).ConfigureAwait(false);
   if(ex is null||ex.Id==0)throw new BibliotecaExemplarInexistenteException(exemplarId);
   if(x.EntidadeId.HasValue&&ex.EntidadeId.HasValue&&ex.EntidadeId.Value!=x.EntidadeId.Value)throw new ExemplarForaDoEscopoException(exemplarId,ex.EntidadeId.Value,x.EntidadeId.Value);
   if(ex.Status=="BLOQUEADO")throw new ExemplarBloqueadoException(exemplarId); // BR-3.4: bloqueio impede emprestimo/reserva
   if(ex.Status is not ("DISPONIVEL" or "ATIVO"))throw new ExemplarNaoDisponivelException(exemplarId,ex.Status ?? "INDEFINIDO");
   return ex;
 }
 async Task ValidarTomadorExistenteAsync(EducacaoAvancadaContext x,long? alunoId,long? servidorId,CancellationToken ct){
   if(alunoId is >0){
     var a=await _context.CreateConnection().ExecuteScalarAsync<long?>(new CommandDefinition("select coalesce(entidade_id,-1) from sigov.aluno where tenant_id=@TenantId and id=@Id and ativo and not is_deleted",new{TenantId=x.TenantId,Id=alunoId.Value},cancellationToken:ct)).ConfigureAwait(false);
     if(a is null)throw new BibliotecaAlunoInexistenteException(alunoId.Value);
     if(a.Value>=0&&x.EntidadeId.HasValue&&a.Value!=x.EntidadeId.Value)throw new ContextoInstitucionalDivergenteException("aluno",alunoId.Value,a.Value,x.EntidadeId.Value);
   }
   if(servidorId is >0){
     var ok=await _context.CreateConnection().ExecuteScalarAsync<bool>(new CommandDefinition("select exists(select 1 from sigov.servidor where tenant_id=@TenantId and id=@Id and ativo and not is_deleted)",new{TenantId=x.TenantId,Id=servidorId.Value},cancellationToken:ct)).ConfigureAwait(false);
     if(!ok)throw new BibliotecaServidorInexistenteException(servidorId.Value);
   }
 }
 public async Task<long> CriarEmprestimoAsync(EducacaoAvancadaContext x,EducacaoBibliotecaCriarEmprestimoRequest r,CancellationToken ct){
   if(!await _inspector.TableExistsAsync("sigov","educacao_biblioteca_emprestimo",ct).ConfigureAwait(false))throw new RecursoNaoDisponivelException("educacao_biblioteca_emprestimo");
   if(r is null)throw new ArgumentException("Emprestimo é obrigatório.");
   if(r.ExemplarId<=0)throw new ArgumentException("Emprestimo exige exemplar.");
   ValidarTomador(r.AlunoId,r.ServidorId);
   var hoje=_clock.DataAtual;
   if(r.Prazo<hoje)throw new PrazoEmprestimoInvalidoException(r.Prazo,hoje);
   var corr=Guid.TryParse(x.CorrelationId,out var g)?g:Guid.NewGuid();
   using var c=(NpgsqlConnection)_context.CreateConnection();
   await c.OpenAsync(ct).ConfigureAwait(false);
   var ex=await CarregarExemplarNoEscopoAsync(x,r.ExemplarId,ct,c).ConfigureAwait(false);
   await ValidarTomadorExistenteAsync(x,r.AlunoId,r.ServidorId,ct).ConfigureAwait(false);
   await using var tx=await c.BeginTransactionAsync(ct).ConfigureAwait(false);
   var vigente=await c.ExecuteScalarAsync<bool>(new CommandDefinition("select exists(select 1 from sigov.educacao_biblioteca_emprestimo where tenant_id=@TenantId and exemplar_id=@ExemplarId and ativo and is_deleted=false and status not in ('ENCERRADA','ENCERRADO','CANCELADA','CANCELADO'))",new{TenantId=x.TenantId,ExemplarId=r.ExemplarId},tx,cancellationToken:ct)).ConfigureAwait(false);
   if(vigente)throw new ExemplarIndisponivelException(r.ExemplarId);
   var maxTomador=await LireParametroEducaoAsync(c,tx,x.TenantId,"EMPRESTIMO_MAX_VIGENTES_TOMADOR",ct).ConfigureAwait(false); // BR-3.1: maximo de emprestimos vigentes por tomador
   var vigentesTomador=await c.ExecuteScalarAsync<long>(new CommandDefinition("select count(1) from sigov.educacao_biblioteca_emprestimo where tenant_id=@TenantId and coalesce(aluno_id,-1)=coalesce(@AlunoId,-1) and coalesce(servidor_id,-1)=coalesce(@ServidorId,-1) and ativo and is_deleted=false and status not in ('ENCERRADA','ENCERRADO','CANCELADA','CANCELADO')",new{TenantId=x.TenantId,AlunoId=r.AlunoId,ServidorId=r.ServidorId},tx,cancellationToken:ct)).ConfigureAwait(false);
   if(vigentesTomador>=maxTomador)throw new EmprestimoTomadorLimiteExcedidoException(r.AlunoId,r.ServidorId,maxTomador);
   long did;
   try{
     did=await c.ExecuteScalarAsync<long>(new CommandDefinition("insert into sigov.educacao_biblioteca_emprestimo(tenant_id,entidade_id,escola_id,exemplar_id,aluno_id,servidor_id,tipo,competencia,data_inicio,data_fim,prazo_original,renovacoes,status,dados,auditoria,correlation_id,created_by) values(@TenantId,@EntidadeId,@EscolaId,@ExemplarId,@AlunoId,@ServidorId,'FISICO',@Hoje,@Hoje,@Prazo,@Prazo,0,'ATIVA','{}'::jsonb,jsonb_build_array(jsonb_build_object('acao','LACAR_EMPRESTIMO','em',now())),@CorrelationId,@UsuarioId) returning id",new{TenantId=x.TenantId,EntidadeId=x.EntidadeId,EscolaId=ex.EscolaId,ExemplarId=r.ExemplarId,AlunoId=r.AlunoId,ServidorId=r.ServidorId,Hoje=hoje,Prazo=r.Prazo,CorrelationId=corr,UsuarioId=x.UsuarioId},tx,cancellationToken:ct)).ConfigureAwait(false);
   }catch(NpgsqlException npe){
     if(npe.SqlState=="23505")throw new ExemplarIndisponivelException(r.ExemplarId);
     throw;
   }
   await tx.CommitAsync(ct).ConfigureAwait(false);
   return did;
 }
 public async Task<long> DevolverEmprestimoAsync(EducacaoAvancadaContext x,long emprestimoId,EducacaoBibliotecaDevolverRequest r,CancellationToken ct){
   if(!await _inspector.TableExistsAsync("sigov","educacao_biblioteca_devolucao",ct).ConfigureAwait(false))throw new RecursoNaoDisponivelException("educacao_biblioteca_devolucao");
   if(!await _inspector.TableExistsAsync("sigov","educacao_biblioteca_emprestimo",ct).ConfigureAwait(false))throw new RecursoNaoDisponivelException("educacao_biblioteca_emprestimo");
   if(!await _inspector.TableExistsAsync("sigov","educacao_biblioteca_reserva",ct).ConfigureAwait(false))throw new RecursoNaoDisponivelException("educacao_biblioteca_reserva");
   if(emprestimoId<=0)throw new ArgumentException("Devolucao exige emprestimo.");
   var hoje=_clock.DataAtual;
   var efetiva=r?.DataDevolucao ?? hoje;
   if(efetiva>hoje)throw new DataDevolucaoInvalidaException(efetiva,hoje); // BR-3.2: data efetiva nao pode ser futura
   var corr=Guid.TryParse(x.CorrelationId,out var g)?g:Guid.NewGuid();
   using var c=(NpgsqlConnection)_context.CreateConnection();
   await c.OpenAsync(ct).ConfigureAwait(false);
   var em=await c.QueryFirstOrDefaultAsync<EducacaoEmprestimoLinha>(new CommandDefinition("select id as Id,entidade_id as EntidadeId,escola_id as EscolaId,exemplar_id as ExemplarId,aluno_id as AlunoId,servidor_id as ServidorId,status as Status,data_fim as DataFim from sigov.educacao_biblioteca_emprestimo where tenant_id=@TenantId and id=@Id and is_deleted=false",new{TenantId=x.TenantId,Id=emprestimoId},cancellationToken:ct)).ConfigureAwait(false);
   if(em is null||em.Id==0)throw new BibliotecaEmprestimoInexistenteException(emprestimoId);
   if(x.EntidadeId.HasValue&&em.EntidadeId.HasValue&&em.EntidadeId.Value!=x.EntidadeId.Value)throw new ContextoInstitucionalDivergenteException("educacao_biblioteca_emprestimo",emprestimoId,em.EntidadeId.Value,x.EntidadeId.Value);
   if(em.Status is not ("ATIVA" or "ATIVO")){
     var eventoOrig=await c.ExecuteScalarAsync<long>(new CommandDefinition("select coalesce(min(id),-1) from sigov.educacao_biblioteca_devolucao where tenant_id=@TenantId and referencia_id=@Id and tipo='DEVOLUCAO' and is_deleted=false",new{TenantId=x.TenantId,Id=emprestimoId},cancellationToken:ct)).ConfigureAwait(false);
     if(eventoOrig>0)return eventoOrig;
     var oc=await c.QueryFirstOrDefaultAsync<(long Ref,long Oid)>(new CommandDefinition("select coalesce(id,0) as Ref,coalesce(id,0) as Oid from sigov.educacao_biblioteca_ocorrencia where tenant_id=@TenantId and referencia_id=@Id and tipo in ('PERDA','DANO','EXTRAVIO') and status='ATIVA' and is_deleted=false order by id limit 1",new{TenantId=x.TenantId,Id=emprestimoId},cancellationToken:ct)).ConfigureAwait(false);
     if(oc.Oid>0)throw new EmprestimoEncerradoPorOcorrenciaException(emprestimoId,oc.Oid);
     throw new EmprestimoJaDevolvidoException(emprestimoId,em.Status ?? "INDEFINIDO");
   }
   await using var tx=await c.BeginTransactionAsync(ct).ConfigureAwait(false);
   var obs=(r?.Observacao ?? "").Trim();
   await c.ExecuteAsync(new CommandDefinition("update sigov.educacao_biblioteca_emprestimo set status='ENCERRADA',justificativa=coalesce(nullif(@Obs,''),'DEVOLUIDO'),updated_at=now(),updated_by=@UsuarioId,auditoria=(case jsonb_typeof(auditoria) when 'array' then auditoria else '[]'::jsonb end)||jsonb_build_array(jsonb_build_object('acao','DEVOLVER_EMPRESTIMO','em',now(),'observacao',@Obs)) where tenant_id=@TenantId and id=@Id",new{Obs=obs,UsuarioId=x.UsuarioId,TenantId=x.TenantId,Id=emprestimoId},tx,cancellationToken:ct)).ConfigureAwait(false);
   long devolucaoId;
   try{
     devolucaoId=await c.ExecuteScalarAsync<long>(new CommandDefinition("insert into sigov.educacao_biblioteca_devolucao(tenant_id,entidade_id,escola_id,exemplar_id,aluno_id,servidor_id,referencia_id,tipo,competencia,status,dados,auditoria,correlation_id,created_by) values(@TenantId,@EntidadeId,@EscolaId,@ExemplarId,@AlunoId,@ServidorId,@EmprestimoId,'DEVOLUCAO',@Efetiva,'ATIVA',jsonb_build_object('em_dia',(coalesce(@DataFim,@Efetiva))>=@Efetiva,'atraso_dias',greatest(0,@Efetiva-(coalesce(@DataFim,@Efetiva)))),jsonb_build_array(jsonb_build_object('acao','REGISTRAR_DEVOLUCAO','em',now())),@CorrelationId,@UsuarioId) returning id",new{TenantId=x.TenantId,EntidadeId=x.EntidadeId,EscolaId=em.EscolaId,ExemplarId=em.ExemplarId,AlunoId=em.AlunoId,ServidorId=em.ServidorId,EmprestimoId=emprestimoId,Efetiva=efetiva,DataFim=em.DataFim,CorrelationId=corr,UsuarioId=x.UsuarioId},tx,cancellationToken:ct)).ConfigureAwait(false);
     var res=await c.QueryFirstOrDefaultAsync<(long Id,long? AlunoId,long? ServidorId,long? EscolaId)>(new CommandDefinition("select id as Id,aluno_id as AlunoId,servidor_id as ServidorId,escola_id as EscolaId from sigov.educacao_biblioteca_reserva where tenant_id=@TenantId and exemplar_id=@ExemplarId and ativo and is_deleted=false and status not in ('ENCERRADA','ENCERRADO','CANCELADA','CANCELADO','CONSUMIDA') order by id limit 1 for update",new{TenantId=x.TenantId,ExemplarId=em.ExemplarId},tx,cancellationToken:ct)).ConfigureAwait(false);
     if(res.Id>0){
       var maxT=await LireParametroEducaoAsync(c,tx,x.TenantId,"EMPRESTIMO_MAX_VIGENTES_TOMADOR",ct).ConfigureAwait(false);
       var vigT=await c.ExecuteScalarAsync<long>(new CommandDefinition("select count(1) from sigov.educacao_biblioteca_emprestimo where tenant_id=@TenantId and coalesce(aluno_id,-1)=coalesce(@AlunoId,-1) and coalesce(servidor_id,-1)=coalesce(@ServidorId,-1) and ativo and is_deleted=false and status not in ('ENCERRADA','ENCERRADO','CANCELADA','CANCELADO')",new{TenantId=x.TenantId,AlunoId=res.AlunoId,ServidorId=res.ServidorId},tx,cancellationToken:ct)).ConfigureAwait(false);
       if(vigT<maxT){
         var prazoPadrao=await LireParametroEducaoAsync(c,tx,x.TenantId,"EMPRESTIMO_PRAZO_PADRAO_DIAS",ct).ConfigureAwait(false);
         var novaFim=hoje.AddDays(prazoPadrao);
         await c.ExecuteAsync(new CommandDefinition("insert into sigov.educacao_biblioteca_emprestimo(tenant_id,entidade_id,escola_id,exemplar_id,aluno_id,servidor_id,tipo,competencia,data_inicio,data_fim,prazo_original,renovacoes,referencia_id,status,dados,auditoria,correlation_id,created_by) values(@TenantId,@EntidadeId,@EscolaId,@ExemplarId,@AlunoId,@ServidorId,'FISICO',@Hoje,@Hoje,@NovaFim,@NovaFim,0,@ReservaId,'ATIVA',jsonb_build_object('origem','RESERVA','reservaId',@ReservaId),jsonb_build_array(jsonb_build_object('acao','TRANSFERIR_RESERVA_EMPRESTIMO','em',now(),'reservaId',@ReservaId)),@CorrelationId,@UsuarioId)",new{TenantId=x.TenantId,EntidadeId=x.EntidadeId,EscolaId=res.EscolaId,ExemplarId=em.ExemplarId,AlunoId=res.AlunoId,ServidorId=res.ServidorId,Hoje=hoje,NovaFim=novaFim,ReservaId=res.Id,CorrelationId=corr,UsuarioId=x.UsuarioId},tx,cancellationToken:ct)).ConfigureAwait(false);
         await c.ExecuteAsync(new CommandDefinition("update sigov.educacao_biblioteca_reserva set status='CONSUMIDA',justificativa='CONSUMIDA_POR_DEVOLUCAO',updated_at=now(),updated_by=@UsuarioId,auditoria=(case jsonb_typeof(auditoria) when 'array' then auditoria else '[]'::jsonb end)||jsonb_build_array(jsonb_build_object('acao','CONSUMIR_RESERVA','em',now())) where tenant_id=@TenantId and id=@Id",new{UsuarioId=x.UsuarioId,TenantId=x.TenantId,Id=res.Id},tx,cancellationToken:ct)).ConfigureAwait(false);
       }else{
         await c.ExecuteAsync(new CommandDefinition("update sigov.educacao_biblioteca_reserva set auditoria=(case jsonb_typeof(auditoria) when 'array' then auditoria else '[]'::jsonb end)||jsonb_build_array(jsonb_build_object('acao','TRANSFERENCIA_PULADA_TOMADOR_MAX_ATINGIDO','em',now())) where tenant_id=@TenantId and id=@Id",new{TenantId=x.TenantId,Id=res.Id},tx,cancellationToken:ct)).ConfigureAwait(false);
       }
     }
   }catch(NpgsqlException npe){
     if(npe.SqlState=="23505"){var ex2=await c.ExecuteScalarAsync<long>(new CommandDefinition("select coalesce(min(id),-1) from sigov.educacao_biblioteca_devolucao where tenant_id=@TenantId and referencia_id=@Id and tipo='DEVOLUCAO' and is_deleted=false",new{TenantId=x.TenantId,Id=emprestimoId},tx,cancellationToken:ct)).ConfigureAwait(false); if(ex2>0){await tx.CommitAsync(ct).ConfigureAwait(false); return ex2;} throw new EmprestimoJaDevolvidoException(emprestimoId,"CONCORRENCIA");}
     throw;
   }
   await tx.CommitAsync(ct).ConfigureAwait(false);
   return devolucaoId;
 }
 public async Task<long> CriarReservaAsync(EducacaoAvancadaContext x,EducacaoBibliotecaCriarReservaRequest r,CancellationToken ct){
   if(!await _inspector.TableExistsAsync("sigov","educacao_biblioteca_reserva",ct).ConfigureAwait(false))throw new RecursoNaoDisponivelException("educacao_biblioteca_reserva");
   if(r is null)throw new ArgumentException("Reserva é obrigatória.");
   if(r.ExemplarId<=0)throw new ArgumentException("Reserva exige exemplar.");
   ValidarTomador(r.AlunoId,r.ServidorId);
   var hoje=_clock.DataAtual;
   var corr=Guid.TryParse(x.CorrelationId,out var g)?g:Guid.NewGuid();
   using var c=(NpgsqlConnection)_context.CreateConnection();
   await c.OpenAsync(ct).ConfigureAwait(false);
   var ex=await CarregarExemplarNoEscopoAsync(x,r.ExemplarId,ct,c).ConfigureAwait(false);
   await ValidarTomadorExistenteAsync(x,r.AlunoId,r.ServidorId,ct).ConfigureAwait(false);
   await using var tx=await c.BeginTransactionAsync(ct).ConfigureAwait(false);
   var vigente=await c.ExecuteScalarAsync<bool>(new CommandDefinition("select exists(select 1 from sigov.educacao_biblioteca_reserva where tenant_id=@TenantId and exemplar_id=@ExemplarId and ativo and is_deleted=false and status not in ('ENCERRADA','ENCERRADO','CANCELADA','CANCELADO'))",new{TenantId=x.TenantId,ExemplarId=r.ExemplarId},tx,cancellationToken:ct)).ConfigureAwait(false);
   if(vigente)throw new ReservaVigenteExisteException(r.ExemplarId);
   long rid;
   try{
     rid=await c.ExecuteScalarAsync<long>(new CommandDefinition("insert into sigov.educacao_biblioteca_reserva(tenant_id,entidade_id,escola_id,exemplar_id,aluno_id,servidor_id,competencia,status,dados,auditoria,correlation_id,created_by) values(@TenantId,@EntidadeId,@EscolaId,@ExemplarId,@AlunoId,@ServidorId,@Hoje,'ATIVA','{}'::jsonb,jsonb_build_array(jsonb_build_object('acao','LACAR_RESERVA','em',now())),@CorrelationId,@UsuarioId) returning id",new{TenantId=x.TenantId,EntidadeId=x.EntidadeId,EscolaId=ex.EscolaId,ExemplarId=r.ExemplarId,AlunoId=r.AlunoId,ServidorId=r.ServidorId,Hoje=hoje,CorrelationId=corr,UsuarioId=x.UsuarioId},tx,cancellationToken:ct)).ConfigureAwait(false);
   }catch(NpgsqlException npe){
     if(npe.SqlState=="23505")throw new ReservaVigenteExisteException(r.ExemplarId);
     throw;
   }
   await tx.CommitAsync(ct).ConfigureAwait(false);
   return rid;
 }
 public async Task<EducacaoBibliotecaDashboardDto> BibliotecaDashboardAsync(long tenantId,CancellationToken ct){
   foreach(var t in new[]{"educacao_biblioteca_acervo","educacao_biblioteca_exemplar","educacao_biblioteca_emprestimo","educacao_biblioteca_reserva"})if(!await _inspector.TableExistsAsync("sigov",t,ct).ConfigureAwait(false))throw new RecursoNaoDisponivelException(t);
   using var c=_context.CreateConnection();
   var sql="select (select count(1) from sigov.educacao_biblioteca_acervo a where a.tenant_id=@TenantId and a.ativo and not a.is_deleted) as Titulos,(select count(1) from sigov.educacao_biblioteca_exemplar e where e.tenant_id=@TenantId and e.ativo and not e.is_deleted and e.status in ('DISPONIVEL','ATIVO') and not exists(select 1 from sigov.educacao_biblioteca_emprestimo m where m.tenant_id=e.tenant_id and m.exemplar_id=e.id and m.ativo and not m.is_deleted and m.status not in ('ENCERRADA','ENCERRADO','CANCELADA','CANCELADO')) and not exists(select 1 from sigov.educacao_biblioteca_reserva rv where rv.tenant_id=e.tenant_id and rv.exemplar_id=e.id and rv.ativo and not rv.is_deleted and rv.status not in ('ENCERRADA','ENCERRADO','CANCELADA','CANCELADO','CONSUMIDA'))) as Disponiveis,(select count(1) from sigov.educacao_biblioteca_emprestimo m where m.tenant_id=@TenantId and m.ativo and not m.is_deleted and m.status not in ('ENCERRADA','ENCERRADO','CANCELADA','CANCELADO')) as EmprestimosAtivos,(select count(1) from sigov.educacao_biblioteca_emprestimo m where m.tenant_id=@TenantId and m.ativo and not m.is_deleted and m.status not in ('ENCERRADA','ENCERRADO','CANCELADA','CANCELADO') and m.data_fim < current_date) as Atrasos,(select count(1) from sigov.educacao_biblioteca_reserva rv where rv.tenant_id=@TenantId and rv.ativo and not rv.is_deleted and rv.status not in ('ENCERRADA','ENCERRADO','CANCELADA','CANCELADO','CONSUMIDA')) as ReservasPendentes,(select count(1) from sigov.educacao_biblioteca_exemplar e where e.tenant_id=@TenantId and e.ativo and not e.is_deleted and e.status='BLOQUEADO') as Bloqueados";
   var d=await c.QueryFirstAsync<EducacaoBibliotecaDashboardDto>(new CommandDefinition(sql,new{TenantId=tenantId},cancellationToken:ct)).ConfigureAwait(false);
   return d;
 }
 // Ciclo 2 Sprint 3 (biblioteca circulacao completa, BR-3.1 a BR-3.8): parametros por tenant,
 // renovacao limitada a partir do prazo original, ocorrencia com compensacao unica, desbloqueio
 // justificavel, inventario fisico com divergencia justificada e timeline append-only. Relogio unico (_clock).
 async Task<int> LireParametroEducaoAsync(NpgsqlConnection c,NpgsqlTransaction? tx,long tenantId,string codigo,CancellationToken ct){
   var v=await c.ExecuteScalarAsync<int?>(new CommandDefinition("select coalesce((select val.valor::text::integer from sigov.parametro_modulo_valor val where val.tenant_id=@TenantId and val.parametro_id=c.parametro_id and not val.is_deleted limit 1),c.valor_padrao::text::integer) from (select id as parametro_id,valor_padrao from sigov.parametro_modulo where modulo='EDUCACAO' and codigo=@Codigo and ativo and not is_deleted limit 1) c",new{TenantId=tenantId,Codigo=codigo},tx,cancellationToken:ct)).ConfigureAwait(false);
   if(v is null||v.Value<=0)throw new RecursoNaoDisponivelException("parametro_modulo.EDUCACAO."+codigo);
   return v.Value;
 }
 async Task<EducacaoExemplarLinha> CarregarExemplarExisteNoEscopoAsync(EducacaoAvancadaContext x,long exemplarId,CancellationToken ct,NpgsqlConnection c){
   var ex=await c.QueryFirstOrDefaultAsync<EducacaoExemplarLinha>(new CommandDefinition("select id as Id,entidade_id as EntidadeId,escola_id as EscolaId,status as Status from sigov.educacao_biblioteca_exemplar where tenant_id=@TenantId and id=@Id and ativo and not is_deleted",new{TenantId=x.TenantId,Id=exemplarId},cancellationToken:ct)).ConfigureAwait(false);
   if(ex is null||ex.Id==0)throw new BibliotecaExemplarInexistenteException(exemplarId);
   if(x.EntidadeId.HasValue&&ex.EntidadeId.HasValue&&ex.EntidadeId.Value!=x.EntidadeId.Value)throw new ExemplarForaDoEscopoException(exemplarId,ex.EntidadeId.Value,x.EntidadeId.Value);
   return ex;
 }
 public async Task<long> RenovarEmprestimoAsync(EducacaoAvancadaContext x,long emprestimoId,CancellationToken ct){
   if(!await _inspector.TableExistsAsync("sigov","educacao_biblioteca_emprestimo",ct).ConfigureAwait(false))throw new RecursoNaoDisponivelException("educacao_biblioteca_emprestimo");
   var corr=Guid.TryParse(x.CorrelationId,out var g)?g:Guid.NewGuid();
   using var c=(NpgsqlConnection)_context.CreateConnection();
   await c.OpenAsync(ct).ConfigureAwait(false);
   var em=await c.QueryFirstOrDefaultAsync<EducacaoEmprestimoLinha>(new CommandDefinition("select id as Id,entidade_id as EntidadeId,escola_id as EscolaId,exemplar_id as ExemplarId,aluno_id as AlunoId,servidor_id as ServidorId,status as Status,data_fim as DataFim,prazo_original as PrazoOriginal,coalesce(renovacoes,0) as Renovacoes from sigov.educacao_biblioteca_emprestimo where tenant_id=@TenantId and id=@Id and is_deleted=false",new{TenantId=x.TenantId,Id=emprestimoId},cancellationToken:ct)).ConfigureAwait(false);
   if(em is null||em.Id==0)throw new BibliotecaEmprestimoInexistenteException(emprestimoId);
   if(x.EntidadeId.HasValue&&em.EntidadeId.HasValue&&em.EntidadeId.Value!=x.EntidadeId.Value)throw new ContextoInstitucionalDivergenteException("educacao_biblioteca_emprestimo",emprestimoId,em.EntidadeId.Value,x.EntidadeId.Value);
   await using var tx=await c.BeginTransactionAsync(ct).ConfigureAwait(false);
   if(em.Status is not ("ATIVA" or "ATIVO"))throw new RenovacaoEmprestimoNaoVigenteException(emprestimoId,em.Status ?? "INDEFINIDO");
   var reservaVig=await c.ExecuteScalarAsync<bool>(new CommandDefinition("select exists(select 1 from sigov.educacao_biblioteca_reserva where tenant_id=@TenantId and exemplar_id=@ExemplarId and ativo and is_deleted=false and status not in ('ENCERRADA','ENCERRADO','CANCELADA','CANCELADO','CONSUMIDA'))",new{TenantId=x.TenantId,ExemplarId=em.ExemplarId},tx,cancellationToken:ct)).ConfigureAwait(false);
   if(reservaVig)throw new RenovacaoReservaVigenteException(em.ExemplarId ?? 0);
   var maxR=await LireParametroEducaoAsync(c,tx,x.TenantId,"RENOVACAO_MAX_POR_EMPRESTIMO",ct).ConfigureAwait(false);
   if(em.Renovacoes>=maxR)throw new RenovacaoLimiteExcedidoException(emprestimoId,em.Renovacoes,maxR);
   var dias=await LireParametroEducaoAsync(c,tx,x.TenantId,"RENOVACAO_DIAS_ESTENDIMENTO",ct).ConfigureAwait(false);
   var basePrazo=em.PrazoOriginal ?? em.DataFim ?? _clock.DataAtual;
   var novoFim=basePrazo.AddDays((em.Renovacoes+1)*dias);
   var deFim=em.DataFim ?? _clock.DataAtual;
   var n=await c.ExecuteAsync(new CommandDefinition("update sigov.educacao_biblioteca_emprestimo set data_fim=@NovoFim,renovacoes=coalesce(renovacoes,0)+1,updated_at=now(),updated_by=@UsuarioId,auditoria=(case jsonb_typeof(auditoria) when 'array' then auditoria else '[]'::jsonb end)||jsonb_build_array(jsonb_build_object('acao','RENOVAR_EMPRESTIMO','de',to_char(@DeFim,'YYYY-MM-DD'),'para',to_char(@NovoFim,'YYYY-MM-DD'))) where tenant_id=@TenantId and id=@Id",new{NovoFim=novoFim,DeFim=deFim,UsuarioId=x.UsuarioId,TenantId=x.TenantId,Id=emprestimoId},tx,cancellationToken:ct)).ConfigureAwait(false);
   if(n!=1)throw new ConflitoDeVersaoEducacaoException("educacao_biblioteca_emprestimo",emprestimoId);
   await c.ExecuteAsync(new CommandDefinition("insert into sigov.educacao_biblioteca_evento(tenant_id,entidade_id,referencia_id,exemplar_id,tipo,nome,competencia,status,dados,auditoria,correlation_id,created_by) values(@TenantId,@EntidadeId,@RefId,@ExemplarId,'RENOVACAO','Renovacao de emprestimo',@Hoje,'ATIVA','{}'::jsonb,jsonb_build_array(jsonb_build_object('acao','RENOVAR_EVENTO','em',now())),@CorrelationId,@UsuarioId)",new{TenantId=x.TenantId,EntidadeId=x.EntidadeId,RefId=emprestimoId,ExemplarId=em.ExemplarId,Hoje=_clock.DataAtual,CorrelationId=corr,UsuarioId=x.UsuarioId},tx,cancellationToken:ct)).ConfigureAwait(false);
   await tx.CommitAsync(ct).ConfigureAwait(false);
   return emprestimoId;
 }
 public async Task<long> LancarOcorrenciaAsync(EducacaoAvancadaContext x,EducacaoBibliotecaOcorrenciaLancarRequest r,CancellationToken ct){
   if(!await _inspector.TableExistsAsync("sigov","educacao_biblioteca_ocorrencia",ct).ConfigureAwait(false))throw new RecursoNaoDisponivelException("educacao_biblioteca_ocorrencia");
   if(!await _inspector.TableExistsAsync("sigov","educacao_biblioteca_exemplar",ct).ConfigureAwait(false))throw new RecursoNaoDisponivelException("educacao_biblioteca_exemplar");
   if(r is null)throw new ArgumentException("Ocorrencia e obrigatoria.");
   var tipo=(r.Tipo ?? "").Trim().ToUpperInvariant();
   if(tipo is not ("PERDA" or "DANO" or "EXTRAVIO" or "BLOQUEIO"))throw new OcorrenciaTipoInvalidoException(r.Tipo ?? "");
   if(string.IsNullOrWhiteSpace(r.Motivo))throw new OcorrenciaMotivoObrigatorioException();
   var hoje=_clock.DataAtual;
   var data=r.Data ?? hoje;
   var corr=Guid.TryParse(x.CorrelationId,out var g)?g:Guid.NewGuid();
   using var c=(NpgsqlConnection)_context.CreateConnection();
   await c.OpenAsync(ct).ConfigureAwait(false);
   var ex=await CarregarExemplarExisteNoEscopoAsync(x,r.ExemplarId,ct,c).ConfigureAwait(false);
   await using var tx=await c.BeginTransactionAsync(ct).ConfigureAwait(false);
   long emprestimoVigente=0;
   if(tipo is ("PERDA" or "DANO" or "EXTRAVIO")){
     emprestimoVigente=await c.ExecuteScalarAsync<long>(new CommandDefinition("select coalesce((select id from sigov.educacao_biblioteca_emprestimo where tenant_id=@TenantId and exemplar_id=@ExemplarId and ativo and is_deleted=false and status in ('ATIVA','ATIVO') order by id limit 1 for update),-1)",new{TenantId=x.TenantId,ExemplarId=r.ExemplarId},tx,cancellationToken:ct)).ConfigureAwait(false);
     if(emprestimoVigente<0)emprestimoVigente=0;
   }
   long? refEmp=emprestimoVigente>0?emprestimoVigente:null;
   long oid;
   oid=await c.ExecuteScalarAsync<long>(new CommandDefinition("insert into sigov.educacao_biblioteca_ocorrencia(tenant_id,entidade_id,escola_id,referencia_id,exemplar_id,tipo,descricao,valor,competencia,status,dados,auditoria,correlation_id,created_by) values(@TenantId,@EntidadeId,@EscolaId,@EmprestimoId,@ExemplarId,@Tipo,@Motivo,@Valor,@Data,'ATIVA','{}'::jsonb,jsonb_build_array(jsonb_build_object('acao','LANCAR_OCORRENCIA','em',now())),@CorrelationId,@UsuarioId) returning id",new{TenantId=x.TenantId,EntidadeId=x.EntidadeId,EscolaId=ex.EscolaId,EmprestimoId=refEmp,ExemplarId=r.ExemplarId,Tipo=tipo,Motivo=r.Motivo.Trim(),Valor=r.ValorEstimado,Data=data,CorrelationId=corr,UsuarioId=x.UsuarioId},tx,cancellationToken:ct)).ConfigureAwait(false);
   if(emprestimoVigente>0){
     var nm=await c.ExecuteAsync(new CommandDefinition("update sigov.educacao_biblioteca_emprestimo set status='ENCERRADA',justificativa='OCORRENCIA:'||@Oid,updated_at=now(),updated_by=@UsuarioId,auditoria=(case jsonb_typeof(auditoria) when 'array' then auditoria else '[]'::jsonb end)||jsonb_build_array(jsonb_build_object('acao','ENCERRAR_POR_OCORRENCIA','ocorrenciaId',@Oid)) where tenant_id=@TenantId and id=@Id",new{Oid=oid,UsuarioId=x.UsuarioId,TenantId=x.TenantId,Id=emprestimoVigente},tx,cancellationToken:ct)).ConfigureAwait(false);
     if(nm!=1)throw new ConflitoDeVersaoEducacaoException("educacao_biblioteca_emprestimo",emprestimoVigente);
   }
   var alvoStatus=tipo=="PERDA"?"PERDIDO":(tipo=="DANO"?"DANIFICADO":(tipo=="EXTRAVIO"?"EXTRAVIADO":"BLOQUEADO"));
   var ne=await c.ExecuteAsync(new CommandDefinition("update sigov.educacao_biblioteca_exemplar set status=@Alvo,justificativa=@Motivo,updated_at=now(),updated_by=@UsuarioId,auditoria=(case jsonb_typeof(auditoria) when 'array' then auditoria else '[]'::jsonb end)||jsonb_build_array(jsonb_build_object('acao','OCORRENCIA_EXEMPLAR','tipoOcorrencia',@Tipo,'ocorrenciaId',@Oid)) where tenant_id=@TenantId and id=@Id",new{Alvo=alvoStatus,Motivo=r.Motivo.Trim(),UsuarioId=x.UsuarioId,Tipo=tipo,Oid=oid,TenantId=x.TenantId,Id=r.ExemplarId},tx,cancellationToken:ct)).ConfigureAwait(false);
   if(ne!=1)throw new ConflitoDeVersaoEducacaoException("educacao_biblioteca_exemplar",r.ExemplarId);
   await tx.CommitAsync(ct).ConfigureAwait(false);
   return oid;
 }
 public async Task<long> EstornarOcorrenciaAsync(EducacaoAvancadaContext x,long ocorrenciaId,EducacaoBibliotecaEstornoOcorrenciaRequest r,CancellationToken ct){
   if(!await _inspector.TableExistsAsync("sigov","educacao_biblioteca_ocorrencia",ct).ConfigureAwait(false))throw new RecursoNaoDisponivelException("educacao_biblioteca_ocorrencia");
   if(!await _inspector.TableExistsAsync("sigov","educacao_biblioteca_exemplar",ct).ConfigureAwait(false))throw new RecursoNaoDisponivelException("educacao_biblioteca_exemplar");
   if(!await _inspector.TableExistsAsync("sigov","educacao_biblioteca_emprestimo",ct).ConfigureAwait(false))throw new RecursoNaoDisponivelException("educacao_biblioteca_emprestimo");
   if(!await _inspector.TableExistsAsync("sigov","educacao_biblioteca_devolucao",ct).ConfigureAwait(false))throw new RecursoNaoDisponivelException("educacao_biblioteca_devolucao");
   if(r is null||string.IsNullOrWhiteSpace(r.Motivo))throw new OcorrenciaEstornoMotivoObrigatorioException();
   using var c=(NpgsqlConnection)_context.CreateConnection();
   await c.OpenAsync(ct).ConfigureAwait(false);
   var oc=await c.QueryFirstOrDefaultAsync<(long Id,long? EntidadeId,long RefId,long? ExemplarId,string Status)>(new CommandDefinition("select id as Id,entidade_id as EntidadeId,coalesce(referencia_id,0) as RefId,exemplar_id as ExemplarId,coalesce(status,'') as Status from sigov.educacao_biblioteca_ocorrencia where tenant_id=@TenantId and id=@Id and is_deleted=false",new{TenantId=x.TenantId,Id=ocorrenciaId},cancellationToken:ct)).ConfigureAwait(false);
   if(oc.Id==0)throw new BibliotecaOcorrenciaInexistenteException(ocorrenciaId);
   if(x.EntidadeId.HasValue&&oc.EntidadeId.HasValue&&oc.EntidadeId.Value!=x.EntidadeId.Value)throw new ContextoInstitucionalDivergenteException("educacao_biblioteca_ocorrencia",ocorrenciaId,oc.EntidadeId.Value,x.EntidadeId.Value);
   if(oc.Status!="ATIVA")return ocorrenciaId;
   await using var tx=await c.BeginTransactionAsync(ct).ConfigureAwait(false);
   var nc=await c.ExecuteAsync(new CommandDefinition("update sigov.educacao_biblioteca_ocorrencia set status='CANCELADA',justificativa=@Motivo,updated_at=now(),updated_by=@UsuarioId,auditoria=(case jsonb_typeof(auditoria) when 'array' then auditoria else '[]'::jsonb end)||jsonb_build_array(jsonb_build_object('acao','ESTORNAR_OCORRENCIA','motivo',@Motivo)) where tenant_id=@TenantId and id=@Id and status='ATIVA'",new{Motivo=r.Motivo.Trim(),UsuarioId=x.UsuarioId,TenantId=x.TenantId,Id=ocorrenciaId},tx,cancellationToken:ct)).ConfigureAwait(false);
   if(nc!=1)throw new ConflitoDeVersaoEducacaoException("educacao_biblioteca_ocorrencia",ocorrenciaId);
   if(oc.ExemplarId.HasValue){
     await c.ExecuteAsync(new CommandDefinition("update sigov.educacao_biblioteca_exemplar set status='DISPONIVEL',justificativa=@Motivo,updated_at=now(),updated_by=@UsuarioId,auditoria=(case jsonb_typeof(auditoria) when 'array' then auditoria else '[]'::jsonb end)||jsonb_build_array(jsonb_build_object('acao','RESTAURAR_EXEMPLAR','ocorrenciaId',@Oid)) where tenant_id=@TenantId and id=@ExemplarId and status in ('PERDIDO','DANIFICADO','EXTRAVIADO','BLOQUEADO')",new{Motivo=r.Motivo.Trim(),UsuarioId=x.UsuarioId,Oid=ocorrenciaId,TenantId=x.TenantId,ExemplarId=oc.ExemplarId},tx,cancellationToken:ct)).ConfigureAwait(false);
   }
   if(oc.RefId>0){
     await c.ExecuteAsync(new CommandDefinition("update sigov.educacao_biblioteca_emprestimo set status='ATIVA',updated_at=now(),updated_by=@UsuarioId,auditoria=(case jsonb_typeof(auditoria) when 'array' then auditoria else '[]'::jsonb end)||jsonb_build_array(jsonb_build_object('acao','REABRIR_POR_ESTORNO_OCORRENCIA','ocorrenciaId',@Oid)) where tenant_id=@TenantId and id=@RefId and status='ENCERRADA' and not exists(select 1 from sigov.educacao_biblioteca_devolucao d where d.tenant_id=@TenantId and d.referencia_id=@RefId and d.tipo='DEVOLUCAO' and d.is_deleted=false)",new{UsuarioId=x.UsuarioId,Oid=ocorrenciaId,TenantId=x.TenantId,RefId=oc.RefId},tx,cancellationToken:ct)).ConfigureAwait(false);
   }
   await tx.CommitAsync(ct).ConfigureAwait(false);
   return ocorrenciaId;
 }
 public async Task<long> DesbloquearExemplarAsync(EducacaoAvancadaContext x,long exemplarId,EducacaoBibliotecaDesbloquearRequest r,CancellationToken ct){
   if(!await _inspector.TableExistsAsync("sigov","educacao_biblioteca_exemplar",ct).ConfigureAwait(false))throw new RecursoNaoDisponivelException("educacao_biblioteca_exemplar");
   if(r is null||string.IsNullOrWhiteSpace(r.Justificativa))throw new DesbloqueioJustificativaObrigatorioException();
   using var c=(NpgsqlConnection)_context.CreateConnection();
   await c.OpenAsync(ct).ConfigureAwait(false);
   var ex=await CarregarExemplarExisteNoEscopoAsync(x,exemplarId,ct,c).ConfigureAwait(false);
   if(ex.Status!="BLOQUEADO")throw new ExemplarNaoBloqueadoException(exemplarId,ex.Status ?? "INDEFINIDO");
   var n=await c.ExecuteAsync(new CommandDefinition("update sigov.educacao_biblioteca_exemplar set status='DISPONIVEL',justificativa=@Just,updated_at=now(),updated_by=@UsuarioId,auditoria=(case jsonb_typeof(auditoria) when 'array' then auditoria else '[]'::jsonb end)||jsonb_build_array(jsonb_build_object('acao','DESBLOQUEAR_EXEMPLAR','motivo',@Just)) where tenant_id=@TenantId and id=@Id",new{Just=r.Justificativa.Trim(),UsuarioId=x.UsuarioId,TenantId=x.TenantId,Id=exemplarId},cancellationToken:ct)).ConfigureAwait(false);
   if(n!=1)throw new ConflitoDeVersaoEducacaoException("educacao_biblioteca_exemplar",exemplarId);
   return exemplarId;
 }
 public async Task<long> CriarInventarioAsync(EducacaoAvancadaContext x,EducacaoBibliotecaInventarioLancarRequest r,CancellationToken ct){
   foreach(var t in new[]{"educacao_biblioteca_inventario","educacao_biblioteca_inventario_item","educacao_biblioteca_exemplar"})if(!await _inspector.TableExistsAsync("sigov",t,ct).ConfigureAwait(false))throw new RecursoNaoDisponivelException(t);
   var hoje=_clock.DataAtual;
   var corr=Guid.TryParse(x.CorrelationId,out var g)?g:Guid.NewGuid();
   var escolaId=r?.EscolaId;
   using var c=(NpgsqlConnection)_context.CreateConnection();
   await c.OpenAsync(ct).ConfigureAwait(false);
   await using var tx=await c.BeginTransactionAsync(ct).ConfigureAwait(false);
   long invId;
   invId=await c.ExecuteScalarAsync<long>(new CommandDefinition("insert into sigov.educacao_biblioteca_inventario(tenant_id,entidade_id,escola_id,codigo,nome,tipo,competencia,status,dados,auditoria,correlation_id,created_by) values(@TenantId,@EntidadeId,@EscolaId,@Codigo,@Nome,'INVENTARIO_FISICO',@Hoje,'ABERTO','{}'::jsonb,jsonb_build_array(jsonb_build_object('acao','CRIAR_INVENTARIO','em',now())),@CorrelationId,@UsuarioId) returning id",new{TenantId=x.TenantId,EntidadeId=x.EntidadeId,EscolaId=escolaId,Codigo="INV-"+hoje.ToString("yyyyMMdd"),Nome="Inventario fisico "+hoje.ToString("yyyy-MM-dd"),Hoje=hoje,CorrelationId=corr,UsuarioId=x.UsuarioId},tx,cancellationToken:ct)).ConfigureAwait(false);
   await c.ExecuteAsync(new CommandDefinition("insert into sigov.educacao_biblioteca_inventario_item(tenant_id,entidade_id,escola_id,referencia_id,exemplar_id,posicao_status,status,dados,auditoria,correlation_id,created_by) select @TenantId,@EntidadeId,e.escola_id,@InvId,e.id,coalesce(e.status,''),'ABERTA','{}'::jsonb,jsonb_build_array(jsonb_build_object('acao','SNAPSHOT_INVENTARIO','em',now())),@CorrelationId,@UsuarioId from sigov.educacao_biblioteca_exemplar e where e.tenant_id=@TenantId and e.ativo and not e.is_deleted and (@EscolaId is null or e.escola_id=@EscolaId)",new{TenantId=x.TenantId,EntidadeId=x.EntidadeId,InvId=invId,EscolaId=escolaId,CorrelationId=corr,UsuarioId=x.UsuarioId},tx,cancellationToken:ct)).ConfigureAwait(false);
   await tx.CommitAsync(ct).ConfigureAwait(false);
   return invId;
 }
 public async Task<long> ContarInventarioItemAsync(EducacaoAvancadaContext x,long inventarioId,EducacaoBibliotecaInventarioContarRequest r,CancellationToken ct){
   foreach(var t in new[]{"educacao_biblioteca_inventario","educacao_biblioteca_inventario_item","educacao_biblioteca_exemplar"})if(!await _inspector.TableExistsAsync("sigov",t,ct).ConfigureAwait(false))throw new RecursoNaoDisponivelException(t);
   if(r is null||r.ExemplarId<=0)throw new ArgumentException("Contagem exige exemplar.");
   if(r.Quantidade<0)throw new InventarioQuantidadeInvalidaException(r.Quantidade);
   var corr=Guid.TryParse(x.CorrelationId,out var g)?g:Guid.NewGuid();
   using var c=(NpgsqlConnection)_context.CreateConnection();
   await c.OpenAsync(ct).ConfigureAwait(false);
   var ex=await CarregarExemplarExisteNoEscopoAsync(x,r.ExemplarId,ct,c).ConfigureAwait(false);
   await using var tx=await c.BeginTransactionAsync(ct).ConfigureAwait(false);
   var inv=await c.QueryFirstOrDefaultAsync<(long Id,string Status)>(new CommandDefinition("select id as Id,coalesce(status,'') as Status from sigov.educacao_biblioteca_inventario where tenant_id=@TenantId and id=@Id and is_deleted=false for update",new{TenantId=x.TenantId,Id=inventarioId},tx,cancellationToken:ct)).ConfigureAwait(false);
   if(inv.Id==0)throw new BibliotecaInventarioInexistenteException(inventarioId);
   if(inv.Status!="ABERTO")throw new InventarioJaFechadoException(inventarioId,inv.Status);
   await c.ExecuteAsync(new CommandDefinition("insert into sigov.educacao_biblioteca_inventario_item(tenant_id,entidade_id,escola_id,referencia_id,exemplar_id,posicao_status,status_contado,quantidade,status,dados,auditoria,correlation_id,created_by) select @TenantId,@EntidadeId,@EscolaId,@InvId,@ExemplarId,(select e.status from sigov.educacao_biblioteca_exemplar e where e.tenant_id=@TenantId and e.id=@ExemplarId),@StatusContado,@Qtd,'ABERTA','{}'::jsonb,jsonb_build_array(jsonb_build_object('acao','CONTAR_ITEM','em',now())),@CorrelationId,@UsuarioId where not exists(select 1 from sigov.educacao_biblioteca_inventario_item i2 where i2.tenant_id=@TenantId and i2.referencia_id=@InvId and i2.exemplar_id=@ExemplarId and not i2.is_deleted)",new{TenantId=x.TenantId,EntidadeId=x.EntidadeId,EscolaId=ex.EscolaId,InvId=inventarioId,ExemplarId=r.ExemplarId,StatusContado=(string?)r.StatusContado,Qtd=r.Quantidade,CorrelationId=corr,UsuarioId=x.UsuarioId},tx,cancellationToken:ct)).ConfigureAwait(false);
   long itemId;
   itemId=await c.ExecuteScalarAsync<long>(new CommandDefinition("update sigov.educacao_biblioteca_inventario_item set quantidade=@Qtd,status_contado=@StatusContado,updated_at=now(),updated_by=@UsuarioId,auditoria=(case jsonb_typeof(auditoria) when 'array' then auditoria else '[]'::jsonb end)||jsonb_build_array(jsonb_build_object('acao','CONTAR_ITEM','em',now())) where tenant_id=@TenantId and referencia_id=@InvId and exemplar_id=@ExemplarId and is_deleted=false returning id",new{Qtd=r.Quantidade,StatusContado=(string?)r.StatusContado,UsuarioId=x.UsuarioId,TenantId=x.TenantId,InvId=inventarioId,ExemplarId=r.ExemplarId},tx,cancellationToken:ct)).ConfigureAwait(false);
   await tx.CommitAsync(ct).ConfigureAwait(false);
   return itemId;
 }
 public async Task<long> JustificarInventarioItemAsync(EducacaoAvancadaContext x,long inventarioId,EducacaoBibliotecaInventarioJustificarRequest r,CancellationToken ct){
   foreach(var t in new[]{"educacao_biblioteca_inventario","educacao_biblioteca_inventario_item"})if(!await _inspector.TableExistsAsync("sigov",t,ct).ConfigureAwait(false))throw new RecursoNaoDisponivelException(t);
   if(r is null||r.ExemplarId<=0)throw new ArgumentException("Justificativa exige exemplar.");
   var desfecho=(r.Desfecho ?? "").Trim().ToUpperInvariant();
   if(desfecho is not ("AJUSTE" or "DISPENSA"))throw new InventarioDesfechoInvalidoException(r.Desfecho ?? "");
   if(string.IsNullOrWhiteSpace(r.Justificativa))throw new InventarioJustificativaObrigatorioException();
   using var c=(NpgsqlConnection)_context.CreateConnection();
   await c.OpenAsync(ct).ConfigureAwait(false);
   await using var tx=await c.BeginTransactionAsync(ct).ConfigureAwait(false);
   var inv=await c.QueryFirstOrDefaultAsync<(long Id,string Status)>(new CommandDefinition("select id as Id,coalesce(status,'') as Status from sigov.educacao_biblioteca_inventario where tenant_id=@TenantId and id=@Id and is_deleted=false for update",new{TenantId=x.TenantId,Id=inventarioId},tx,cancellationToken:ct)).ConfigureAwait(false);
   if(inv.Id==0)throw new BibliotecaInventarioInexistenteException(inventarioId);
   if(inv.Status!="ABERTO")throw new InventarioJaFechadoException(inventarioId,inv.Status);
   var item=await c.QueryFirstOrDefaultAsync<(long Id,decimal? Qtd,string? PosStatus,string? StContado,string Desfecho)>(new CommandDefinition("select id as Id,quantidade as Qtd,posicao_status as PosStatus,status_contado as StContado,coalesce(desfecho,'') as Desfecho from sigov.educacao_biblioteca_inventario_item where tenant_id=@TenantId and referencia_id=@InvId and exemplar_id=@ExemplarId and is_deleted=false for update",new{TenantId=x.TenantId,InvId=inventarioId,ExemplarId=r.ExemplarId},tx,cancellationToken:ct)).ConfigureAwait(false);
   if(item.Id==0)throw new BibliotecaInventarioItemInexistenteException(inventarioId,r.ExemplarId);
   var divergente=item.Desfecho=="" && item.Qtd.HasValue && ((item.Qtd.Value==0m && item.PosStatus!=null) || (item.Qtd.Value>1m) || (item.Qtd.Value>0m && item.PosStatus==null) || (!string.IsNullOrEmpty(item.StContado) && !string.IsNullOrEmpty(item.PosStatus) && !string.Equals(item.StContado.Trim().ToUpperInvariant(),item.PosStatus.Trim().ToUpperInvariant(),StringComparison.Ordinal)));
   if(!divergente)throw new InventarioItemSemDivergenciaException(inventarioId,r.ExemplarId);
   var nj=await c.ExecuteAsync(new CommandDefinition("update sigov.educacao_biblioteca_inventario_item set desfecho=@Desfecho,justificativa=@Just,status='FECHADA',updated_at=now(),updated_by=@UsuarioId,auditoria=(case jsonb_typeof(auditoria) when 'array' then auditoria else '[]'::jsonb end)||jsonb_build_array(jsonb_build_object('acao','JUSTIFICAR_DIVERGENCIA','desfecho',@Desfecho)) where tenant_id=@TenantId and id=@Id",new{Desfecho=desfecho,Just=r.Justificativa.Trim(),UsuarioId=x.UsuarioId,TenantId=x.TenantId,Id=item.Id},tx,cancellationToken:ct)).ConfigureAwait(false);
   if(nj!=1)throw new ConflitoDeVersaoEducacaoException("educacao_biblioteca_inventario_item",item.Id);
   if(desfecho=="AJUSTE"){
     if(item.Qtd.HasValue && item.Qtd.Value==0m && item.PosStatus!=null){
       await c.ExecuteAsync(new CommandDefinition("update sigov.educacao_biblioteca_exemplar set status='PERDIDO',justificativa=@Just,updated_at=now(),updated_by=@UsuarioId,auditoria=(case jsonb_typeof(auditoria) when 'array' then auditoria else '[]'::jsonb end)||jsonb_build_array(jsonb_build_object('acao','AJUSTE_INVENTARIO','inventarioId',@InvId)) where tenant_id=@TenantId and id=@ExemplarId and status in ('DISPONIVEL','ATIVO')",new{Just=r.Justificativa.Trim(),UsuarioId=x.UsuarioId,InvId=inventarioId,TenantId=x.TenantId,ExemplarId=r.ExemplarId},tx,cancellationToken:ct)).ConfigureAwait(false);
     }else if(item.Qtd.HasValue && item.Qtd.Value==1m && !string.IsNullOrEmpty(item.StContado) && item.StContado.Trim().ToUpperInvariant()=="DANIFICADO"){
       await c.ExecuteAsync(new CommandDefinition("update sigov.educacao_biblioteca_exemplar set status='DANIFICADO',justificativa=@Just,updated_at=now(),updated_by=@UsuarioId,auditoria=(case jsonb_typeof(auditoria) when 'array' then auditoria else '[]'::jsonb end)||jsonb_build_array(jsonb_build_object('acao','AJUSTE_INVENTARIO','inventarioId',@InvId)) where tenant_id=@TenantId and id=@ExemplarId and status in ('DISPONIVEL','ATIVO')",new{Just=r.Justificativa.Trim(),UsuarioId=x.UsuarioId,InvId=inventarioId,TenantId=x.TenantId,ExemplarId=r.ExemplarId},tx,cancellationToken:ct)).ConfigureAwait(false);
     }
   }
   await tx.CommitAsync(ct).ConfigureAwait(false);
   return item.Id;
 }
 public async Task<long> FecharInventarioAsync(EducacaoAvancadaContext x,long inventarioId,string? justificativa,CancellationToken ct){
   foreach(var t in new[]{"educacao_biblioteca_inventario","educacao_biblioteca_inventario_item"})if(!await _inspector.TableExistsAsync("sigov",t,ct).ConfigureAwait(false))throw new RecursoNaoDisponivelException(t);
   using var c=(NpgsqlConnection)_context.CreateConnection();
   await c.OpenAsync(ct).ConfigureAwait(false);
   await using var tx=await c.BeginTransactionAsync(ct).ConfigureAwait(false);
   var inv=await c.QueryFirstOrDefaultAsync<(long Id,string Status)>(new CommandDefinition("select id as Id,coalesce(status,'') as Status from sigov.educacao_biblioteca_inventario where tenant_id=@TenantId and id=@Id and is_deleted=false for update",new{TenantId=x.TenantId,Id=inventarioId},tx,cancellationToken:ct)).ConfigureAwait(false);
   if(inv.Id==0)throw new BibliotecaInventarioInexistenteException(inventarioId);
   if(inv.Status!="ABERTO")throw new InventarioJaFechadoException(inventarioId,inv.Status);
   var abertas=await c.ExecuteScalarAsync<long>(new CommandDefinition("select count(1) from sigov.educacao_biblioteca_inventario_item i where i.tenant_id=@TenantId and i.referencia_id=@InvId and i.is_deleted=false and i.desfecho is null and i.quantidade is not null and ((i.quantidade=0 and i.posicao_status is not null) or (i.quantidade>1) or (i.quantidade>0 and i.posicao_status is null) or (i.status_contado is not null and i.posicao_status is not null and upper(i.status_contado)<>upper(i.posicao_status)))",new{TenantId=x.TenantId,InvId=inventarioId},tx,cancellationToken:ct)).ConfigureAwait(false);
   if(abertas>0)throw new InventarioDivergenciasAbertasException(inventarioId,abertas);
   var nf=await c.ExecuteAsync(new CommandDefinition("update sigov.educacao_biblioteca_inventario set status='FECHADO',justificativa=@Jus,updated_at=now(),updated_by=@UsuarioId,auditoria=(case jsonb_typeof(auditoria) when 'array' then auditoria else '[]'::jsonb end)||jsonb_build_array(jsonb_build_object('acao','FECHAR_INVENTARIO')) where tenant_id=@TenantId and id=@Id and status='ABERTO'",new{Jus=string.IsNullOrWhiteSpace(justificativa)?(string?)null:justificativa.Trim(),UsuarioId=x.UsuarioId,TenantId=x.TenantId,Id=inventarioId},tx,cancellationToken:ct)).ConfigureAwait(false);
   if(nf!=1)throw new ConflitoDeVersaoEducacaoException("educacao_biblioteca_inventario",inventarioId);
   await tx.CommitAsync(ct).ConfigureAwait(false);
   return inventarioId;
 }
 public async Task<List<EducacaoBibliotecaHistoricoItemDto>> HistoricoExemplarAsync(EducacaoAvancadaContext x,long exemplarId,CancellationToken ct){
   foreach(var t in new[]{"educacao_biblioteca_exemplar","educacao_biblioteca_emprestimo","educacao_biblioteca_devolucao","educacao_biblioteca_reserva","educacao_biblioteca_ocorrencia","educacao_biblioteca_evento"})if(!await _inspector.TableExistsAsync("sigov",t,ct).ConfigureAwait(false))throw new RecursoNaoDisponivelException(t);
   using var c=(NpgsqlConnection)_context.CreateConnection();
   await c.OpenAsync(ct).ConfigureAwait(false);
   var existe=await c.ExecuteScalarAsync<long>(new CommandDefinition("select coalesce(min(id),-1) from sigov.educacao_biblioteca_exemplar where tenant_id=@TenantId and id=@Id and ativo and not is_deleted",new{TenantId=x.TenantId,Id=exemplarId},cancellationToken:ct)).ConfigureAwait(false);
   if(existe<0)throw new BibliotecaExemplarInexistenteException(exemplarId);
   var sql="select em.created_at as Em,'EMPRESTIMO' as Tipo,'emprestimo '||em.id||' ('||coalesce(em.status,'')||')' as Descricao,em.id as ReferenciaId from sigov.educacao_biblioteca_emprestimo em where em.tenant_id=@TenantId and em.exemplar_id=@Id and not em.is_deleted union all select dv.created_at,'DEVOLUCAO','devolucao '||dv.id||' (atraso '||coalesce(dv.dados->>'atraso_dias','0')||' dia(s))',dv.id from sigov.educacao_biblioteca_devolucao dv where dv.tenant_id=@TenantId and dv.exemplar_id=@Id and not dv.is_deleted union all select rs.created_at,'RESERVA','reserva '||rs.id||' ('||coalesce(rs.status,'')||')',rs.id from sigov.educacao_biblioteca_reserva rs where rs.tenant_id=@TenantId and rs.exemplar_id=@Id and not rs.is_deleted union all select oc.created_at,'OCORRENCIA',coalesce(oc.tipo,'')||' ('||coalesce(oc.status,'')||') '||coalesce(oc.descricao,''),oc.id from sigov.educacao_biblioteca_ocorrencia oc where oc.tenant_id=@TenantId and oc.exemplar_id=@Id and not oc.is_deleted union all select ev.created_at,'EVENTO',coalesce(ev.nome,ev.tipo),ev.id from sigov.educacao_biblioteca_evento ev where ev.tenant_id=@TenantId and ev.exemplar_id=@Id and not ev.is_deleted order by 1 desc limit 200";
   var rows=await c.QueryAsync<EducacaoBibliotecaHistoricoItemDto>(new CommandDefinition(sql,new{TenantId=x.TenantId,Id=exemplarId},cancellationToken:ct)).ConfigureAwait(false);
   return new List<EducacaoBibliotecaHistoricoItemDto>(rows);
 }
 public async Task<List<EducacaoBibliotecaHistoricoItemDto>> HistoricoTomadorAsync(EducacaoAvancadaContext x,long? alunoId,long? servidorId,CancellationToken ct){
   foreach(var t in new[]{"educacao_biblioteca_emprestimo","educacao_biblioteca_devolucao","educacao_biblioteca_reserva"})if(!await _inspector.TableExistsAsync("sigov",t,ct).ConfigureAwait(false))throw new RecursoNaoDisponivelException(t);
   ValidarTomador(alunoId,servidorId);
   using var c=(NpgsqlConnection)_context.CreateConnection();
   await c.OpenAsync(ct).ConfigureAwait(false);
   var sql="select em.created_at as Em,'EMPRESTIMO' as Tipo,'emprestimo '||em.id||' ('||coalesce(em.status,'')||')' as Descricao,em.id as ReferenciaId from sigov.educacao_biblioteca_emprestimo em where em.tenant_id=@TenantId and (@AlunoId is null or em.aluno_id=@AlunoId) and (@ServidorId is null or em.servidor_id=@ServidorId) and not em.is_deleted union all select dv.created_at,'DEVOLUCAO','devolucao '||dv.id||' (atraso '||coalesce(dv.dados->>'atraso_dias','0')||' dia(s))',dv.id from sigov.educacao_biblioteca_devolucao dv where dv.tenant_id=@TenantId and (@AlunoId is null or dv.aluno_id=@AlunoId) and (@ServidorId is null or dv.servidor_id=@ServidorId) and not dv.is_deleted union all select rs.created_at,'RESERVA','reserva '||rs.id||' ('||coalesce(rs.status,'')||')',rs.id from sigov.educacao_biblioteca_reserva rs where rs.tenant_id=@TenantId and (@AlunoId is null or rs.aluno_id=@AlunoId) and (@ServidorId is null or rs.servidor_id=@ServidorId) and not rs.is_deleted order by 1 desc limit 200";
   var rows=await c.QueryAsync<EducacaoBibliotecaHistoricoItemDto>(new CommandDefinition(sql,new{TenantId=x.TenantId,AlunoId=alunoId,ServidorId=servidorId},cancellationToken:ct)).ConfigureAwait(false);
   return new List<EducacaoBibliotecaHistoricoItemDto>(rows);
 }
 private sealed class EducacaoRotaLinha{public long Id{get;set;} public long? EntidadeId{get;set;} public long? EscolaId{get;set;} public string? Turno{get;set;} public string? Status{get;set;}}
 private sealed class EducacaoPontoLinha{public long Id{get;set;} public long? RotaId{get;set;}}
 async Task<EducacaoRotaLinha> CarregarRotaNoEscopoAsync(EducacaoAvancadaContext x,long rotaId,CancellationToken ct,NpgsqlConnection c){
   var r=await c.QueryFirstOrDefaultAsync<EducacaoRotaLinha>(new CommandDefinition("select id as Id,entidade_id as EntidadeId,escola_id as EscolaId,turno as Turno,status as Status from sigov.educacao_transporte_rota where tenant_id=@TenantId and id=@Id and ativo and not is_deleted",new{TenantId=x.TenantId,Id=rotaId},cancellationToken:ct)).ConfigureAwait(false);
   if(r is null)throw new TransporteRotaInexistenteException(rotaId);
   if(r.EntidadeId>=0&&x.EntidadeId.HasValue&&r.EntidadeId.Value!=x.EntidadeId.Value)throw new ContextoInstitucionalDivergenteException("educacao_transporte_rota",rotaId,r.EntidadeId.Value,x.EntidadeId.Value);
   return r;
 }
 public async Task<long> LancarRotaAsync(EducacaoAvancadaContext x,EducacaoTransporteLancarRotaRequest r,CancellationToken ct){
   if(!await _inspector.TableExistsAsync("sigov","educacao_transporte_rota",ct).ConfigureAwait(false))throw new RecursoNaoDisponivelException("educacao_transporte_rota");
   if(r is null)throw new ArgumentException("Rota é obrigatória.");
   if(string.IsNullOrWhiteSpace(r.Nome))throw new ArgumentException("Rota exige nome.");
   if(string.IsNullOrWhiteSpace(r.Turno))throw new ArgumentException("Rota exige turno.");
   var temEscola=r.EscolaId is >0;var temRegiao=!string.IsNullOrWhiteSpace(r.Regiao);
   if(temEscola==temRegiao)throw new TransporteRotaEscolaOuRegiaoInvalidoException();
   if(temEscola)await ValidarEscopoInstitucionalAsync(x.TenantId,x.EntidadeId,r.EscolaId,null,ct).ConfigureAwait(false);
   var turno=r.Turno.Trim().ToUpperInvariant();
   var corr=Guid.TryParse(x.CorrelationId,out var g)?g:Guid.NewGuid();
   using var c=_context.CreateConnection();
   return await c.ExecuteScalarAsync<long>(new CommandDefinition("insert into sigov.educacao_transporte_rota(tenant_id,entidade_id,escola_id,codigo,nome,tipo,turno,status,dados,auditoria,correlation_id,created_by) values(@TenantId,@EntidadeId,@EscolaId,@Codigo,@Nome,'ESCOLAR',@Turno,'ATIVA',(case when @Regiao is null then '{}'::jsonb else jsonb_build_object('regiao',@Regiao) end),jsonb_build_array(jsonb_build_object('acao','LANCAR_ROTA','em',now())),@CorrelationId,@UsuarioId) returning id",new{TenantId=x.TenantId,EntidadeId=x.EntidadeId,EscolaId=r.EscolaId,Codigo=string.IsNullOrWhiteSpace(r.Codigo)?(string?)null:r.Codigo.Trim(),Nome=r.Nome.Trim(),Turno=turno,Regiao=r.Regiao?.Trim(),CorrelationId=corr,UsuarioId=x.UsuarioId},cancellationToken:ct)).ConfigureAwait(false);
 }
 public async Task<long> LancarPontoAsync(EducacaoAvancadaContext x,long rotaId,EducacaoTransporteLancarPontoRequest r,CancellationToken ct){
   if(!await _inspector.TableExistsAsync("sigov","educacao_transporte_rota_ponto",ct).ConfigureAwait(false))throw new RecursoNaoDisponivelException("educacao_transporte_rota_ponto");
   if(r is null)throw new ArgumentException("Ponto é obrigatório.");
   if(string.IsNullOrWhiteSpace(r.Nome))throw new ArgumentException("Ponto exige nome.");
   if(r.Ordem<=0)throw new TransportePontoOrdemInvalidaException(r.Ordem);
   var corr=Guid.TryParse(x.CorrelationId,out var g)?g:Guid.NewGuid();
   using var c=(NpgsqlConnection)_context.CreateConnection();
   await c.OpenAsync(ct).ConfigureAwait(false);
   var rota=await CarregarRotaNoEscopoAsync(x,rotaId,ct,c).ConfigureAwait(false);
   if(rota.Status is not ("ATIVA" or "ATIVO"))throw new TransporteRotaNaoVigenteException(rotaId,rota.Status ?? "INDEFINIDO");
   await using var tx=await c.BeginTransactionAsync(ct).ConfigureAwait(false);
   var ocupado=await c.ExecuteScalarAsync<bool>(new CommandDefinition("select exists(select 1 from sigov.educacao_transporte_rota_ponto where tenant_id=@TenantId and coalesce(rota_id,(0)::bigint)=coalesce(@RotaId,(0)::bigint) and coalesce(numero,(0)::bigint)=coalesce(@Ordem,(0)::bigint) and ativo and is_deleted=false and status not in ('ENCERRADA','ENCERRADO','CANCELADA','CANCELADO'))",new{TenantId=x.TenantId,RotaId=rotaId,Ordem=r.Ordem},tx,cancellationToken:ct)).ConfigureAwait(false);
   if(ocupado)throw new TransportePontoOrdemExistenteException(rotaId,r.Ordem);
   long pid;
   try{
     pid=await c.ExecuteScalarAsync<long>(new CommandDefinition("insert into sigov.educacao_transporte_rota_ponto(tenant_id,entidade_id,escola_id,rota_id,nome,numero,turno,status,descricao,auditoria,correlation_id,created_by) values(@TenantId,@EntidadeId,@EscolaId,@RotaId,@Nome,@Ordem,@Turno,'ATIVA',@Descricao,jsonb_build_array(jsonb_build_object('acao','LANCAR_PONTO','em',now())),@CorrelationId,@UsuarioId) returning id",new{TenantId=x.TenantId,EntidadeId=rota.EntidadeId,EscolaId=rota.EscolaId,RotaId=rotaId,Nome=r.Nome.Trim(),Ordem=r.Ordem,Turno=rota.Turno,Descricao=string.IsNullOrWhiteSpace(r.Descricao)?(string?)null:r.Descricao.Trim(),CorrelationId=corr,UsuarioId=x.UsuarioId},tx,cancellationToken:ct)).ConfigureAwait(false);
   }catch(NpgsqlException npe){
     if(npe.SqlState=="23505")throw new TransportePontoOrdemExistenteException(rotaId,r.Ordem);
     throw;
   }
   await tx.CommitAsync(ct).ConfigureAwait(false);
   return pid;
 }
 public async Task<long> VincularAlunoAsync(EducacaoAvancadaContext x,long rotaId,EducacaoTransporteVincularAlunoRequest r,CancellationToken ct){
   if(!await _inspector.TableExistsAsync("sigov","educacao_transporte_aluno",ct).ConfigureAwait(false))throw new RecursoNaoDisponivelException("educacao_transporte_aluno");
   if(r is null)throw new ArgumentException("Vínculo de aluno é obrigatório.");
   if(r.AlunoId<=0)throw new ArgumentException("Vínculo exige aluno.");
   var corr=Guid.TryParse(x.CorrelationId,out var g)?g:Guid.NewGuid();
   using var c=(NpgsqlConnection)_context.CreateConnection();
   await c.OpenAsync(ct).ConfigureAwait(false);
   var rota=await CarregarRotaNoEscopoAsync(x,rotaId,ct,c).ConfigureAwait(false);
   if(rota.Status is not ("ATIVA" or "ATIVO"))throw new TransporteRotaNaoVigenteException(rotaId,rota.Status ?? "INDEFINIDO");
   var turno=rota.Turno;
   if(!string.IsNullOrWhiteSpace(r.Turno)){
     var tReq=r.Turno.Trim().ToUpperInvariant();
     if(turno is null||tReq!=turno.Trim().ToUpperInvariant())throw new TransporteTurnoVinculoDivergenteException(r.Turno.Trim(),turno ?? "(sem turno)");
     turno=tReq;
   }
   var a=await c.ExecuteScalarAsync<long?>(new CommandDefinition("select coalesce(entidade_id,-1) from sigov.aluno where tenant_id=@TenantId and id=@Id and ativo and not is_deleted",new{TenantId=x.TenantId,Id=r.AlunoId},cancellationToken:ct)).ConfigureAwait(false);
   if(a is null)throw new TransporteAlunoInexistenteException(r.AlunoId);
   if(a.Value>=0&&x.EntidadeId.HasValue&&a.Value!=x.EntidadeId.Value)throw new ContextoInstitucionalDivergenteException("aluno",r.AlunoId,a.Value,x.EntidadeId.Value);
   long? pontoId=null;
   if(r.PontoId is >0){
     var p=await c.QueryFirstOrDefaultAsync<EducacaoPontoLinha>(new CommandDefinition("select id as Id,rota_id as RotaId from sigov.educacao_transporte_rota_ponto where tenant_id=@TenantId and id=@Id and ativo and not is_deleted",new{TenantId=x.TenantId,Id=r.PontoId.Value},cancellationToken:ct)).ConfigureAwait(false);
     if(p is null)throw new TransportePontoInexistenteException(r.PontoId.Value);
     if(p.RotaId is null||p.RotaId.Value!=rotaId)throw new TransportePontoRotaDivergenteException(r.PontoId.Value,p.RotaId??0,rotaId);
     pontoId=r.PontoId.Value;
   }
   await using var tx=await c.BeginTransactionAsync(ct).ConfigureAwait(false);
   var vinculado=await c.ExecuteScalarAsync<bool>(new CommandDefinition("select exists(select 1 from sigov.educacao_transporte_aluno where tenant_id=@TenantId and coalesce(rota_id,(0)::bigint)=coalesce(@RotaId,(0)::bigint) and coalesce(aluno_id,(0)::bigint)=coalesce(@AlunoId,(0)::bigint) and turno=coalesce(@Turno,'') and ativo and is_deleted=false and status not in ('ENCERRADA','ENCERRADO','CANCELADA','CANCELADO'))",new{TenantId=x.TenantId,RotaId=rotaId,AlunoId=r.AlunoId,Turno=turno},tx,cancellationToken:ct)).ConfigureAwait(false);
   if(vinculado)throw new TransporteAlunoJaVinculadoARotaException(r.AlunoId,rotaId,turno ?? "(sem turno)");
   long vid;
   try{
     vid=await c.ExecuteScalarAsync<long>(new CommandDefinition("insert into sigov.educacao_transporte_aluno(tenant_id,entidade_id,escola_id,aluno_id,rota_id,referencia_id,turno,status,auditoria,correlation_id,created_by) values(@TenantId,@EntidadeId,@EscolaId,@AlunoId,@RotaId,@PontoId,@Turno,'ATIVA',jsonb_build_array(jsonb_build_object('acao','VINCULAR_ALUNO','em',now())),@CorrelationId,@UsuarioId) returning id",new{TenantId=x.TenantId,EntidadeId=rota.EntidadeId,EscolaId=rota.EscolaId,AlunoId=r.AlunoId,RotaId=rotaId,PontoId=pontoId,Turno=turno,CorrelationId=corr,UsuarioId=x.UsuarioId},tx,cancellationToken:ct)).ConfigureAwait(false);
   }catch(NpgsqlException npe){
     if(npe.SqlState=="23505")throw new TransporteAlunoJaVinculadoARotaException(r.AlunoId,rotaId,turno ?? "(sem turno)");
     throw;
   }
   await tx.CommitAsync(ct).ConfigureAwait(false);
   return vid;
 }
 // Ciclo 2 Sprint 4 (transporte escolar: da rota à viagem, BR-4.1 a BR-4.8): veiculo apto por
// situacao documental/inspecao parametrizavel, motorista por habilitacao+catalogo de compatibilidade
// configuravel, viagem sem sobreposicao (backstops parciais veiculo x data / motorista x data),
// presenca com fechamento e estorno unitario, ocorrencia com gravidade/desfecho/carona de horas,
// custo estimado no planejamento + realizado imutavel via comando proprio, ciclo PROGRAMADA ->
// EM_ANDAMENTO -> CONCLUIDA com cancelamento/substituicao auditados. Relogio unico (_clock),
// falhas nomeadas (regra 13), escopo tenant x entidade (regras 21-24).
private sealed class EducacaoViagemLinha{public long Id{get;set;} public long? EntidadeId{get;set;} public long? EscolaId{get;set;} public long? RotaId{get;set;} public long? VeiculoId{get;set;} public long? MotoristaId{get;set;} public long? MonitorId{get;set;} public string? Status{get;set;} public DateOnly? DataInicio{get;set;} public DateTime? HoraInicio{get;set;} public int Versao{get;set;} public bool PresencaFechada{get;set;} public decimal KmEstimado{get;set;} public decimal TarifaKm{get;set;} public decimal CombEstimado{get;set;} public decimal PedagioPrevisto{get;set;}}
private sealed class EducacaoVeiculoLinha{public long Id{get;set;} public long? EntidadeId{get;set;} public string? Situacao{get;set;} public DateOnly? Inspecao{get;set;}}
private sealed class EducacaoMotoristaLinha{public long Id{get;set;} public long? EntidadeId{get;set;} public string? Categoria{get;set;} public DateOnly? Validade{get;set;}}
private sealed class EducacaoCustoResumoLinha{public decimal Estimado{get;set;} public decimal? Realizado{get;set;}}
async Task<string> LireParametroTextoEducaoAsync(NpgsqlConnection c,NpgsqlTransaction? tx,long tenantId,string codigo,CancellationToken ct){
  var v=await c.ExecuteScalarAsync<string?>(new CommandDefinition("select coalesce((select case when jsonb_typeof(val.valor)='string' then val.valor#>>'{}' else val.valor::text end from sigov.parametro_modulo_valor val where val.tenant_id=@TenantId and val.parametro_id=c.parametro_id and not val.is_deleted limit 1),case when jsonb_typeof(c.valor_padrao)='string' then c.valor_padrao#>>'{}' else c.valor_padrao::text end) from (select id as parametro_id,valor_padrao from sigov.parametro_modulo where modulo='EDUCACAO' and codigo=@Codigo and ativo and not is_deleted limit 1) c",new{TenantId=tenantId,Codigo=codigo},tx,cancellationToken:ct)).ConfigureAwait(false);
  if(string.IsNullOrWhiteSpace(v))throw new RecursoNaoDisponivelException("parametro_modulo.EDUCACAO."+codigo);
  return v.Trim();
}
async Task<EducacaoVeiculoLinha> CarregarVeiculoNoEscopoAsync(EducacaoAvancadaContext x,long veiculoId,CancellationToken ct,NpgsqlConnection c){
  var v=await c.QueryFirstOrDefaultAsync<EducacaoVeiculoLinha>(new CommandDefinition("select id as Id,entidade_id as EntidadeId,situacao_documental as Situacao,inspecao_validade as Inspecao from sigov.educacao_transporte_veiculo where tenant_id=@TenantId and id=@Id and ativo and not is_deleted",new{TenantId=x.TenantId,Id=veiculoId},cancellationToken:ct)).ConfigureAwait(false);
  if(v is null||v.Id==0)throw new TransporteVeiculoInexistenteException(veiculoId);
  if(x.EntidadeId.HasValue&&v.EntidadeId.HasValue&&v.EntidadeId.Value!=x.EntidadeId.Value)throw new ContextoInstitucionalDivergenteException("educacao_transporte_veiculo",veiculoId,v.EntidadeId.Value,x.EntidadeId.Value);
  return v;
}
async Task<(long Id,long? EntidadeId,string? Categoria,DateOnly? Validade)> CarregarMotoristaNoEscopoAsync(EducacaoAvancadaContext x,long motoristaId,CancellationToken ct,NpgsqlConnection c){
  var m=await c.QueryFirstOrDefaultAsync<(long Id,long? EntidadeId,string? Categoria,DateOnly? Validade)>(new CommandDefinition("select id as Id,entidade_id as EntidadeId,coalesce(habilitacao_categoria,'') as Categoria,habilitacao_validade as Validade from sigov.educacao_transporte_motorista where tenant_id=@TenantId and id=@Id and ativo and not is_deleted",new{TenantId=x.TenantId,Id=motoristaId},cancellationToken:ct)).ConfigureAwait(false);
  if(m.Item1==0)throw new TransporteMotoristaInexistenteException(motoristaId);
  if(x.EntidadeId.HasValue&&m.EntidadeId.HasValue&&m.EntidadeId.Value!=x.EntidadeId.Value)throw new ContextoInstitucionalDivergenteException("educacao_transporte_motorista",motoristaId,m.EntidadeId.Value,x.EntidadeId.Value);
  return (m.Id,m.EntidadeId,m.Categoria,m.Validade);
}
async Task<EducacaoViagemLinha> CarregarViagemNoEscopoAsync(EducacaoAvancadaContext x,long viagemId,CancellationToken ct,NpgsqlConnection c){
  var v=await c.QueryFirstOrDefaultAsync<EducacaoViagemLinha>(new CommandDefinition("select id as Id,entidade_id as EntidadeId,escola_id as EscolaId,rota_id as RotaId,veiculo_id as VeiculoId,motorista_id as MotoristaId,monitor_id as MonitorId,status as Status,data_inicio as DataInicio,hora_inicio as HoraInicio,coalesce(versao,1) as Versao,coalesce(presenca_fechada,false) as PresencaFechada,coalesce(km_estimado,0) as KmEstimado,coalesce(tarifa_km,0) as TarifaKm,coalesce(combustivel_estimado,0) as CombEstimado,coalesce(pedagio_previsto,0) as PedagioPrevisto from sigov.educacao_transporte_viagem where tenant_id=@TenantId and id=@Id and is_deleted=false",new{TenantId=x.TenantId,Id=viagemId},cancellationToken:ct)).ConfigureAwait(false);
  if(v is null||v.Id==0)throw new TransporteViagemInexistenteException(viagemId);
  if(x.EntidadeId.HasValue&&v.EntidadeId.HasValue&&v.EntidadeId.Value!=x.EntidadeId.Value)throw new ContextoInstitucionalDivergenteException("educacao_transporte_viagem",viagemId,v.EntidadeId.Value,x.EntidadeId.Value);
  return v;
}
// BR-4.1: inspecao vencida (validade < data operacional) segue o modo parametrico do tenant —
// ALERTA prossegue a alocacao, BLOQUEIO impede a viagem (falha nomeada).
async Task ValidarInspecaoVeiculoAsync(NpgsqlConnection c,NpgsqlTransaction? tx,long tenantId,long veiculoId,DateOnly? validade,CancellationToken ct){
  if(validade is null||validade.Value>=_clock.DataAtual)return;
  var modo=await LireParametroTextoEducaoAsync(c,tx,tenantId,"TRANSPORTE_INSPECCAO_MODO",ct).ConfigureAwait(false);
  if(modo=="BLOQUEIO")throw new TransporteVeiculoInspecaoVencidaException(veiculoId,validade.Value);
}
public async Task<long> LancarVeiculoAsync(EducacaoAvancadaContext x,EducacaoTransporteLancarVeiculoRequest r,CancellationToken ct){
  if(!await _inspector.TableExistsAsync("sigov","educacao_transporte_veiculo",ct).ConfigureAwait(false))throw new RecursoNaoDisponivelException("educacao_transporte_veiculo");
  if(r is null)throw new ArgumentException("Veiculo é obrigatório.");
  var placa=(r.Placa ?? "").Trim().ToUpperInvariant();
  if(string.IsNullOrWhiteSpace(placa))throw new ArgumentException("Veículo exige placa.");
  if(r.Capacidade<=0)throw new TransporteVeiculoCapacidadeInvalidaException(r.Capacidade);
  var classe=(r.Classe ?? "").Trim().ToUpperInvariant();
  if(string.IsNullOrWhiteSpace(classe))throw new ArgumentException("Veículo exige classe.");
  var situ=(r.SituacaoDocumental ?? "REGULAR").Trim().ToUpperInvariant();
  if(situ is not ("REGULAR" or "VENCIDA" or "BAIXA"))throw new TransporteVeiculoSituacaoInvalidaException(r.SituacaoDocumental ?? "(nulo)");
  // BR-4.1: sem validade informada, a inspecao nasce vencendo em N meses (parâmetro do tenant)
  DateOnly? inspecao=r.InspecaoValidade;
  var corr=Guid.TryParse(x.CorrelationId,out var g)?g:Guid.NewGuid();
  using var c=(NpgsqlConnection)_context.CreateConnection();
  await c.OpenAsync(ct).ConfigureAwait(false);
  if(inspecao is null){var meses=await LireParametroEducaoAsync(c,null,x.TenantId,"TRANSPORTE_INSPECCAO_VALIDADE_MESES",ct).ConfigureAwait(false); inspecao=_clock.DataAtual.AddMonths(meses);}
  try{
    return await c.ExecuteScalarAsync<long>(new CommandDefinition("insert into sigov.educacao_transporte_veiculo(tenant_id,entidade_id,codigo,nome,placa,capacidade,categoria,situacao_documental,inspecao_validade,status,dados,auditoria,correlation_id,created_by) values(@TenantId,@EntidadeId,@Placa,@Placa,@Placa,@Capacidade,@Classe,@Situacao,@Inspecao,'ATIVA',jsonb_build_object('classe',@Classe),jsonb_build_array(jsonb_build_object('acao','LANCAR_VEICULO','em',now())),@CorrelationId,@UsuarioId) returning id",new{TenantId=x.TenantId,EntidadeId=x.EntidadeId,Placa=placa,Capacidade=r.Capacidade,Classe=classe,Situacao=situ,Inspecao=inspecao,CorrelationId=corr,UsuarioId=x.UsuarioId},cancellationToken:ct)).ConfigureAwait(false);
  }catch(NpgsqlException npe){
    if(npe.SqlState=="23505")throw new TransporteVeiculoPlacaExistenteException(placa);
    throw;
  }
}
public async Task<long> LancarMotoristaAsync(EducacaoAvancadaContext x,EducacaoTransporteLancarMotoristaRequest r,CancellationToken ct){
  if(!await _inspector.TableExistsAsync("sigov","educacao_transporte_motorista",ct).ConfigureAwait(false))throw new RecursoNaoDisponivelException("educacao_transporte_motorista");
  if(r is null)throw new ArgumentException("Motorista é obrigatório.");
  if(string.IsNullOrWhiteSpace(r.Nome))throw new ArgumentException("Motorista exige nome.");
  var hab=(r.HabilitacaoCategoria ?? "").Trim().ToUpperInvariant();
  if(hab.Length!=1||hab[0]<'A'||hab[0]>'E')throw new ArgumentException($"Categoria de habilitação '{r.HabilitacaoCategoria ?? "(nulo)"}' inválida; use uma das categorias canônicas A a E.");
  var corr=Guid.TryParse(x.CorrelationId,out var g)?g:Guid.NewGuid();
  using var c=_context.CreateConnection();
  return await c.ExecuteScalarAsync<long>(new CommandDefinition("insert into sigov.educacao_transporte_motorista(tenant_id,entidade_id,nome,habilitacao_categoria,habilitacao_validade,status,dados,auditoria,correlation_id,created_by) values(@TenantId,@EntidadeId,@Nome,@Habilidade,@Validade,'ATIVA',jsonb_build_object('categoria',@Habilidade),jsonb_build_array(jsonb_build_object('acao','LANCAR_MOTORISTA','em',now())),@CorrelationId,@UsuarioId) returning id",new{TenantId=x.TenantId,EntidadeId=x.EntidadeId,Nome=r.Nome.Trim(),Habilidade=hab,Validade=r.HabilitacaoValidade,CorrelationId=corr,UsuarioId=x.UsuarioId},cancellationToken:ct)).ConfigureAwait(false);
}
public async Task<long> LancarViagemAsync(EducacaoAvancadaContext x,EducacaoTransporteLancarViagemRequest r,CancellationToken ct){
  if(!await _inspector.TableExistsAsync("sigov","educacao_transporte_viagem",ct).ConfigureAwait(false))throw new RecursoNaoDisponivelException("educacao_transporte_viagem");
  if(!await _inspector.TableExistsAsync("sigov","educacao_transporte_custo",ct).ConfigureAwait(false))throw new RecursoNaoDisponivelException("educacao_transporte_custo");
  if(r is null)throw new ArgumentException("Viagem é obrigatória.");
  if(r.RotaId<=0||r.VeiculoId<=0||r.MotoristaId<=0)throw new ArgumentException("Viagem exige rota, veículo e motorista.");
  if(r.Data<_clock.DataAtual)throw new ArgumentException($"Data da viagem {r.Data:yyyy-MM-dd} anterior à data operacional {_clock.DataAtual:yyyy-MM-dd}; programe viagens a partir de hoje.");
  if(r.KmEstimado<0m||r.TarifaKm<0m||r.CombustivelEstimado<0m||r.PedagioPrevisto<0m)throw new ArgumentException("Componentes da estimativa de custo devem ser não negativos.");
  var corr=Guid.TryParse(x.CorrelationId,out var g)?g:Guid.NewGuid();
  using var c=(NpgsqlConnection)_context.CreateConnection();
  await c.OpenAsync(ct).ConfigureAwait(false);
  var rota=await CarregarRotaNoEscopoAsync(x,r.RotaId,ct,c).ConfigureAwait(false);
  if(rota.Status is not ("ATIVA" or "ATIVO"))throw new TransporteRotaNaoVigenteException(r.RotaId,rota.Status ?? "INDEFINIDO");
  // BR-4.1 aptidao do veiculo (situacao documental + inspecao com modo configuravel)
  var veic=await CarregarVeiculoNoEscopoAsync(x,r.VeiculoId,ct,c).ConfigureAwait(false);
  if(veic.Situacao is not null&&veic.Situacao!="REGULAR")throw new TransporteVeiculoInaptoException(r.VeiculoId,veic.Situacao);
  await ValidarInspecaoVeiculoAsync(c,null,x.TenantId,r.VeiculoId,veic.Inspecao,ct).ConfigureAwait(false);
  // BR-4.2 aptidao do motorista (habilitacao na data da viagem + categoria compativel com a classe)
  var moto=await CarregarMotoristaNoEscopoAsync(x,r.MotoristaId,ct,c).ConfigureAwait(false);
  if(moto.Item4 is not null&&moto.Item4.Value<r.Data)throw new TransporteMotoristaHabilitacaoVencidaException(r.MotoristaId,moto.Item4.Value,r.Data);
  var classe=await c.ExecuteScalarAsync<string?>(new CommandDefinition("select coalesce(nullif(categoria,''),'(sem classe)') from sigov.educacao_transporte_veiculo where tenant_id=@TenantId and id=@Id",new{TenantId=x.TenantId,Id=r.VeiculoId},cancellationToken:ct)).ConfigureAwait(false);
  if(string.IsNullOrWhiteSpace(moto.Item3)||!await c.ExecuteScalarAsync<bool>(new CommandDefinition("select exists(select 1 from sigov.transporte_categoria_compatibilidade where categoria_habilitacao=@Hab and classe_veiculo=@Classe and ativo and not is_deleted)",new{Hab=moto.Item3,Classe=classe},cancellationToken:ct)).ConfigureAwait(false))throw new TransporteMotoristaCategoriaIncompativelException(moto.Item3 ?? "(nulo)",classe ?? "(nulo)");
  // BR-4.3 monitor quando exigido pela esfera/jurisdicao do tenant (parâmetro)
  var monObrig=await LireParametroTextoEducaoAsync(c,null,x.TenantId,"TRANSPORTE_MONITOR_OBRIGATORIO",ct).ConfigureAwait(false);
  if(monObrig is ("TRUE" or "true" or "1")&&(r.MonitorIds is null||!r.MonitorIds.Any()))throw new TransporteViagemMonitorObrigatorioException();
  // BR-4.3 capacidade: vinculados ativos da rota <= capacidade do veiculo
  var vinc=await c.ExecuteScalarAsync<int>(new CommandDefinition("select count(1) from sigov.educacao_transporte_aluno where tenant_id=@TenantId and coalesce(rota_id,(0)::bigint)=coalesce(@RotaId,(0)::bigint) and ativo and is_deleted=false and status in ('ATIVA','ATIVO')",new{TenantId=x.TenantId,RotaId=r.RotaId},cancellationToken:ct)).ConfigureAwait(false);
  var cap=await c.ExecuteScalarAsync<int?>(new CommandDefinition("select capacidade from sigov.educacao_transporte_veiculo where tenant_id=@TenantId and id=@Id",new{TenantId=x.TenantId,Id=r.VeiculoId},cancellationToken:ct)).ConfigureAwait(false);
  if(cap is >0&&vinc>cap.Value)throw new TransporteViagemCapacidadeExcedidaException(cap.Value,vinc);
  // BR-4.5 carona de ocorrencia grave do veiculo (janela parametrica apos desfecho)
  var horas=await LireParametroEducaoAsync(c,null,x.TenantId,"TRANSPORTE_OCORRENCIA_GRAVE_CARONA_HORAS",ct).ConfigureAwait(false);
  var ocGrave=await c.ExecuteScalarAsync<long>(new CommandDefinition($"select coalesce(max(o.id),(0)::bigint) from sigov.educacao_transporte_ocorrencia o join sigov.educacao_transporte_viagem v on v.id=o.referencia_id and v.tenant_id=o.tenant_id where o.tenant_id=@TenantId and v.veiculo_id=@Veiculo and o.gravidade='ALTA' and o.ativo and not o.is_deleted and not v.is_deleted and (o.desfecho_em is null or o.desfecho_em > now() - make_interval(hours => @Horas))",new{TenantId=x.TenantId,Veiculo=r.VeiculoId,Horas=horas},cancellationToken:ct)).ConfigureAwait(false);
  if(ocGrave>0)throw new TransporteViagemOcorrenciaGraveCaronaException(r.VeiculoId,ocGrave,horas);
  await using var tx=await c.BeginTransactionAsync(ct).ConfigureAwait(false);
  long vid;
  var monLista=r.MonitorIds is null?Array.Empty<long>():r.MonitorIds.ToArray();
  try{
    vid=await c.ExecuteScalarAsync<long>(new CommandDefinition("insert into sigov.educacao_transporte_viagem(tenant_id,entidade_id,escola_id,rota_id,veiculo_id,motorista_id,monitor_id,turno,competencia,data_inicio,km_estimado,tarifa_km,combustivel_estimado,pedagio_previsto,versao,status,dados,auditoria,correlation_id,created_by) values(@TenantId,@EntidadeId,@EscolaId,@RotaId,@VeiculoId,@MotoristaId,@MonitorId,@Turno,@Data,@Data,@KmEst,@Tarifa,@Comb,@Pedagio,1,'PROGRAMADA',jsonb_build_object('monitores',@MonitorsJson::jsonb,'tarifaKm',@Tarifa,'combustivelEstimado',@Comb,'pedagioPrevisto',@Pedagio,'kmEstimado',@KmEst),jsonb_build_array(jsonb_build_object('acao','LANCAR_VIAGEM','em',now())),@CorrelationId,@UsuarioId) returning id",new{TenantId=x.TenantId,EntidadeId=rota.EntidadeId,EscolaId=rota.EscolaId,RotaId=r.RotaId,VeiculoId=r.VeiculoId,MotoristaId=r.MotoristaId,MonitorId=monLista.Length>0?monLista[0]:(long?)null,Turno=rota.Turno,Data=r.Data,KmEst=r.KmEstimado,Tarifa=r.TarifaKm,Comb=r.CombustivelEstimado,Pedagio=r.PedagioPrevisto,MonitorsJson=JsonSerializer.Serialize(monLista),CorrelationId=corr,UsuarioId=x.UsuarioId},tx,cancellationToken:ct)).ConfigureAwait(false);
  }catch(NpgsqlException npe){
    // transacao PG ja esta abortada apos o 23505: o diagnostico nao pode usar a tx (25P02);
    // mapeia pela constraint violada, presente na mensagem do servidor.
    if(npe.SqlState=="23505"){
      var msg=npe.Message ?? "";
      if(msg.Contains("ux_transporte_viagem_tenant_motorista_data"))throw new TransporteViagemMotoristaSobreposicaoException(r.MotoristaId,r.Data);
      throw new TransporteViagemVeiculoSobreposicaoException(r.VeiculoId,r.Data);
    }
    throw;
  }
  // BR-4.6 estimativa gravada no planejamento (km x tarifa + combustivel + pedagio)
  var estim=r.KmEstimado*r.TarifaKm+r.CombustivelEstimado+r.PedagioPrevisto;
  await c.ExecuteAsync(new CommandDefinition("insert into sigov.educacao_transporte_custo(tenant_id,entidade_id,escola_id,referencia_id,rota_id,veiculo_id,motorista_id,competencia,tipo,valor,descricao,dados,auditoria,correlation_id,created_by) values(@TenantId,@EntidadeId,@EscolaId,@ViagemId,@RotaId,@VeiculoId,@MotoristaId,@Data,'ESTIMADO',@Valor,'ESTIMATIVA_PLANEJAMENTO',jsonb_build_object('kmEstimado',@KmEst,'tarifaKm',@Tarifa,'combustivelEstimado',@Comb,'pedagioPrevisto',@Pedagio),jsonb_build_array(jsonb_build_object('acao','GRANAVAR_CUSTO_ESTIMADO','em',now())),@CorrelationId,@UsuarioId)",new{TenantId=x.TenantId,EntidadeId=rota.EntidadeId,EscolaId=rota.EscolaId,ViagemId=vid,RotaId=r.RotaId,VeiculoId=r.VeiculoId,MotoristaId=r.MotoristaId,Data=r.Data,Valor=estim,KmEst=r.KmEstimado,Tarifa=r.TarifaKm,Comb=r.CombustivelEstimado,Pedagio=r.PedagioPrevisto,CorrelationId=corr,UsuarioId=x.UsuarioId},tx,cancellationToken:ct)).ConfigureAwait(false);
  await tx.CommitAsync(ct).ConfigureAwait(false);
  return vid;
}
   static DateTime ComoUtc(DateTime d)=>d.Kind switch{DateTimeKind.Utc=>d,DateTimeKind.Local=>d.ToUniversalTime(),_=>DateTime.SpecifyKind(d,DateTimeKind.Utc)};
public async Task<long> IniciarViagemAsync(EducacaoAvancadaContext x,long viagemId,EducacaoTransporteIniciarViagemRequest r,CancellationToken ct){
  if(!await _inspector.TableExistsAsync("sigov","educacao_transporte_viagem",ct).ConfigureAwait(false))throw new RecursoNaoDisponivelException("educacao_transporte_viagem");
  if(r is null||r.HoraInicio is null)throw new ArgumentException("Iniciar viagem exige hora de início.");
  var corr=Guid.TryParse(x.CorrelationId,out var g)?g:Guid.NewGuid();
  using var c=(NpgsqlConnection)_context.CreateConnection();
  await c.OpenAsync(ct).ConfigureAwait(false);
  var v=await CarregarViagemNoEscopoAsync(x,viagemId,ct,c).ConfigureAwait(false);
  var hora=ComoUtc(r.HoraInicio.Value);
  await using var tx=await c.BeginTransactionAsync(ct).ConfigureAwait(false);
  if(v.Status is not "PROGRAMADA")throw new TransporteViagemEstadoInvalidoException(viagemId,"INICIAR",v.Status ?? "INDEFINIDO");
  var n=await c.ExecuteAsync(new CommandDefinition("update sigov.educacao_transporte_viagem set status='EM_ANDAMENTO',hora_inicio=@Hora,updated_at=now(),updated_by=@UsuarioId,auditoria=(case jsonb_typeof(auditoria) when 'array' then auditoria else '[]'::jsonb end)||jsonb_build_array(jsonb_build_object('acao','INICIAR_VIAGEM','em',now())) where tenant_id=@TenantId and id=@Id",new{Hora=hora,UsuarioId=x.UsuarioId,TenantId=x.TenantId,Id=viagemId},tx,cancellationToken:ct)).ConfigureAwait(false);
  if(n!=1)throw new ConflitoDeVersaoEducacaoException("educacao_transporte_viagem",viagemId);
  await tx.CommitAsync(ct).ConfigureAwait(false);
  return viagemId;
}
public async Task<long> ConcluirViagemAsync(EducacaoAvancadaContext x,long viagemId,EducacaoTransporteConcluirViagemRequest r,CancellationToken ct){
  if(!await _inspector.TableExistsAsync("sigov","educacao_transporte_viagem",ct).ConfigureAwait(false))throw new RecursoNaoDisponivelException("educacao_transporte_viagem");
  if(r is null||r.HoraFim is null)throw new ArgumentException("Concluir viagem exige hora de fim.");
  var km=r.KmPercorridos ?? 0m;
  if(km<0m)throw new ArgumentException($"Km percorridos {km:0.##} inválidos; use um valor não negativo.");
  var corr=Guid.TryParse(x.CorrelationId,out var g)?g:Guid.NewGuid();
  using var c=(NpgsqlConnection)_context.CreateConnection();
  await c.OpenAsync(ct).ConfigureAwait(false);
  var v=await CarregarViagemNoEscopoAsync(x,viagemId,ct,c).ConfigureAwait(false);
  var fim=ComoUtc(r.HoraFim.Value);
  await using var tx=await c.BeginTransactionAsync(ct).ConfigureAwait(false);
  if(v.Status is not ("EM_ANDAMENTO"))throw new TransporteViagemEstadoInvalidoException(viagemId,"CONCLUIR",v.Status ?? "INDEFINIDO");
  var n=await c.ExecuteAsync(new CommandDefinition("update sigov.educacao_transporte_viagem set status='CONCLUIDA',hora_fim=@Fim,km_percorridos=@Km,updated_at=now(),updated_by=@UsuarioId,auditoria=(case jsonb_typeof(auditoria) when 'array' then auditoria else '[]'::jsonb end)||jsonb_build_array(jsonb_build_object('acao','CONCLUIR_VIAGEM','em',now(),'kmPercorridos',@Km)) where tenant_id=@TenantId and id=@Id",new{Fim=fim,Km=km,UsuarioId=x.UsuarioId,TenantId=x.TenantId,Id=viagemId},tx,cancellationToken:ct)).ConfigureAwait(false);
  if(n!=1)throw new ConflitoDeVersaoEducacaoException("educacao_transporte_viagem",viagemId);
  await tx.CommitAsync(ct).ConfigureAwait(false);
  return viagemId;
}
public async Task<long> CancelarViagemAsync(EducacaoAvancadaContext x,long viagemId,EducacaoTransporteCancelarViagemRequest r,CancellationToken ct){
  if(!await _inspector.TableExistsAsync("sigov","educacao_transporte_viagem",ct).ConfigureAwait(false))throw new RecursoNaoDisponivelException("educacao_transporte_viagem");
  if(r is null)throw new ArgumentException("Cancelamento é obrigatório.");
  var just=(r.Justificativa ?? "").Trim();
  if(just.Length==0)throw new TransporteViagemCancelamentoJustificativaObrigatorioException();
  var corr=Guid.TryParse(x.CorrelationId,out var g)?g:Guid.NewGuid();
  using var c=(NpgsqlConnection)_context.CreateConnection();
  await c.OpenAsync(ct).ConfigureAwait(false);
  var v=await CarregarViagemNoEscopoAsync(x,viagemId,ct,c).ConfigureAwait(false);
  if(v.Status is ("CONCLUIDA" or "CANCELADA" or "SUBSTITUIDA"))throw new TransporteViagemEstadoInvalidoException(viagemId,"CANCELAR",v.Status);
  await using var tx=await c.BeginTransactionAsync(ct).ConfigureAwait(false);
  if(v.Status!="PROGRAMADA"){
    if(r.OcorrenciaId is not >0)throw new TransporteViagemCancelamentoExigeOcorrenciaException(viagemId,v.Status ?? "INDEFINIDO");
    var oc=await c.ExecuteScalarAsync<long>(new CommandDefinition("select coalesce(count(1),(0)::bigint) from sigov.educacao_transporte_ocorrencia where tenant_id=@TenantId and id=@Oc and referencia_id=@Viagem and ativo and not is_deleted",new{TenantId=x.TenantId,Oc=r.OcorrenciaId.Value,Viagem=viagemId},tx,cancellationToken:ct)).ConfigureAwait(false);
    if(oc!=1)throw new TransporteOcorrenciaInexistenteException(r.OcorrenciaId.Value);
  }
  var n=await c.ExecuteAsync(new CommandDefinition("update sigov.educacao_transporte_viagem set status='CANCELADA',justificativa=@Just,updated_at=now(),updated_by=@UsuarioId,auditoria=(case jsonb_typeof(auditoria) when 'array' then auditoria else '[]'::jsonb end)||jsonb_build_array(jsonb_build_object('acao','CANCELAR_VIAGEM','em',now(),'ocorrenciaId',@Oc)) where tenant_id=@TenantId and id=@Id",new{Just=just,Oc=r.OcorrenciaId,UsuarioId=x.UsuarioId,TenantId=x.TenantId,Id=viagemId},tx,cancellationToken:ct)).ConfigureAwait(false);
  if(n!=1)throw new ConflitoDeVersaoEducacaoException("educacao_transporte_viagem",viagemId);
  await tx.CommitAsync(ct).ConfigureAwait(false);
  return viagemId;
}
public async Task<long> SubstituirViagemAsync(EducacaoAvancadaContext x,long viagemId,EducacaoTransporteSubstituirViagemRequest r,CancellationToken ct){
  if(!await _inspector.TableExistsAsync("sigov","educacao_transporte_viagem",ct).ConfigureAwait(false))throw new RecursoNaoDisponivelException("educacao_transporte_viagem");
  if(r is null)throw new ArgumentException("Substituição é obrigatória.");
  if(r.VeiculoId is null&&r.MotoristaId is null)throw new TransporteViagemSubstituicaoSemRecursoException();
  var just=(r.Justificativa ?? "").Trim();
  if(just.Length==0)throw new ArgumentException("Substituição exige justificativa.");
  var corr=Guid.TryParse(x.CorrelationId,out var g)?g:Guid.NewGuid();
  using var c=(NpgsqlConnection)_context.CreateConnection();
  await c.OpenAsync(ct).ConfigureAwait(false);
  var v=await CarregarViagemNoEscopoAsync(x,viagemId,ct,c).ConfigureAwait(false);
  if(v.Status is not ("PROGRAMADA" or "EM_ANDAMENTO"))throw new TransporteViagemEstadoInvalidoException(viagemId,"SUBSTITUIR",v.Status ?? "INDEFINIDO");
  var novoVeic=r.VeiculoId ?? v.VeiculoId ?? 0;
  var novoMoto=r.MotoristaId ?? v.MotoristaId ?? 0;
  var data=v.DataInicio ?? _clock.DataAtual;
  // aptidao do recurso novo (mesmas regras do lancamento: BR-4.1/BR-4.2)
  var veic=await CarregarVeiculoNoEscopoAsync(x,novoVeic,ct,c).ConfigureAwait(false);
  if(veic.Situacao is not null&&veic.Situacao!="REGULAR")throw new TransporteVeiculoInaptoException(novoVeic,veic.Situacao);
  await ValidarInspecaoVeiculoAsync(c,null,x.TenantId,novoVeic,veic.Inspecao,ct).ConfigureAwait(false);
  var moto=await CarregarMotoristaNoEscopoAsync(x,novoMoto,ct,c).ConfigureAwait(false);
  if(moto.Item4 is not null&&moto.Item4.Value<data)throw new TransporteMotoristaHabilitacaoVencidaException(novoMoto,moto.Item4.Value,data);
  var classe=await c.ExecuteScalarAsync<string?>(new CommandDefinition("select coalesce(nullif(categoria,''),'(sem classe)') from sigov.educacao_transporte_veiculo where tenant_id=@TenantId and id=@Id",new{TenantId=x.TenantId,Id=novoVeic},cancellationToken:ct)).ConfigureAwait(false);
  if(string.IsNullOrWhiteSpace(moto.Item3)||!await c.ExecuteScalarAsync<bool>(new CommandDefinition("select exists(select 1 from sigov.transporte_categoria_compatibilidade where categoria_habilitacao=@Hab and classe_veiculo=@Classe and ativo and not is_deleted)",new{Hab=moto.Item3,Classe=classe},cancellationToken:ct)).ConfigureAwait(false))throw new TransporteMotoristaCategoriaIncompativelException(moto.Item3 ?? "(nulo)",classe ?? "(nulo)");
  // sobreposicao sem a viagem em si (backstops parciais; BR-4.3)
  await using var tx=await c.BeginTransactionAsync(ct).ConfigureAwait(false);
  var vdup=await c.ExecuteScalarAsync<bool>(new CommandDefinition("select exists(select 1 from sigov.educacao_transporte_viagem where tenant_id=@TenantId and coalesce(veiculo_id,(0)::bigint)=coalesce(@Veiculo,(0)::bigint) and data_inicio=@Data and id<>@Self and ativo and is_deleted=false and status in ('PROGRAMADA','EM_ANDAMENTO'))",new{TenantId=x.TenantId,Veiculo=novoVeic,Data=data,Self=viagemId},tx,cancellationToken:ct)).ConfigureAwait(false);
  if(vdup)throw new TransporteViagemVeiculoSobreposicaoException(novoVeic,data);
  var mdup=await c.ExecuteScalarAsync<bool>(new CommandDefinition("select exists(select 1 from sigov.educacao_transporte_viagem where tenant_id=@TenantId and coalesce(motorista_id,(0)::bigint)=coalesce(@Moto,(0)::bigint) and data_inicio=@Data and id<>@Self and ativo and is_deleted=false and status in ('PROGRAMADA','EM_ANDAMENTO'))",new{TenantId=x.TenantId,Moto=novoMoto,Data=data,Self=viagemId},tx,cancellationToken:ct)).ConfigureAwait(false);
  if(mdup)throw new TransporteViagemMotoristaSobreposicaoException(novoMoto,data);
  long novoId;
  try{
    novoId=await c.ExecuteScalarAsync<long>(new CommandDefinition("insert into sigov.educacao_transporte_viagem(tenant_id,entidade_id,escola_id,rota_id,veiculo_id,motorista_id,monitor_id,turno,competencia,data_inicio,hora_inicio,km_estimado,tarifa_km,combustivel_estimado,pedagio_previsto,versao,referencia_id,status,dados,auditoria,correlation_id,created_by) select tenant_id,entidade_id,escola_id,rota_id,@Veiculo,@Motorista,monitor_id,turno,competencia,data_inicio,hora_inicio,km_estimado,tarifa_km,combustivel_estimado,pedagio_previsto,coalesce(versao,1)+1,@Origem,status,jsonb_build_object('substituicao',to_jsonb(@Just)),jsonb_build_array(jsonb_build_object('acao','SUBSTITUIR_VIAGEM','em',now(),'origem',@Origem::text,'justificativa',@Just)),@CorrelationId,@UsuarioId from sigov.educacao_transporte_viagem where tenant_id=@TenantId and id=@Origem returning id",new{Veiculo=novoVeic,Motorista=novoMoto,Just=just,Origem=viagemId,TenantId=x.TenantId,CorrelationId=corr,UsuarioId=x.UsuarioId},tx,cancellationToken:ct)).ConfigureAwait(false);
  }catch(NpgsqlException npe){
    if(npe.SqlState=="23505")throw vdup?new TransporteViagemVeiculoSobreposicaoException(novoVeic,data):new TransporteViagemMotoristaSobreposicaoException(novoMoto,data);
    throw;
  }
  var n=await c.ExecuteAsync(new CommandDefinition("update sigov.educacao_transporte_viagem set status='SUBSTITUIDA',justificativa=@Just,updated_at=now(),updated_by=@UsuarioId,auditoria=(case jsonb_typeof(auditoria) when 'array' then auditoria else '[]'::jsonb end)||jsonb_build_array(jsonb_build_object('acao','SUBSTITUIDA_POR','em',now(),'novaViagem',@NovoId,'justificativa',@Just)) where tenant_id=@TenantId and id=@Origem",new{Just=just,NovoId=novoId,UsuarioId=x.UsuarioId,TenantId=x.TenantId,Origem=viagemId},tx,cancellationToken:ct)).ConfigureAwait(false);
  if(n!=1)throw new ConflitoDeVersaoEducacaoException("educacao_transporte_viagem",viagemId);
  // presencas e custos seguem a nova versao (a viagem original fica SUBSTITUIDA - cronologia append-only)
  await c.ExecuteAsync(new CommandDefinition("update sigov.educacao_transporte_frequencia set referencia_id=@NovoId,updated_at=now() where tenant_id=@TenantId and referencia_id=@Origem and tipo='PRESENCA_TRANSPORTE' and ativo and is_deleted=false",new{NovoId=novoId,Origem=viagemId,TenantId=x.TenantId},tx,cancellationToken:ct)).ConfigureAwait(false);
  await c.ExecuteAsync(new CommandDefinition("update sigov.educacao_transporte_custo set referencia_id=@NovoId,updated_at=now() where tenant_id=@TenantId and referencia_id=@Origem and ativo and is_deleted=false",new{NovoId=novoId,Origem=viagemId,TenantId=x.TenantId},tx,cancellationToken:ct)).ConfigureAwait(false);
  await tx.CommitAsync(ct).ConfigureAwait(false);
  return novoId;
}
public async Task<long> LancarPresencaAsync(EducacaoAvancadaContext x,long viagemId,EducacaoTransporteLancarPresencaRequest r,CancellationToken ct){
  if(!await _inspector.TableExistsAsync("sigov","educacao_transporte_frequencia",ct).ConfigureAwait(false))throw new RecursoNaoDisponivelException("educacao_transporte_frequencia");
  if(!await _inspector.TableExistsAsync("sigov","educacao_transporte_aluno",ct).ConfigureAwait(false))throw new RecursoNaoDisponivelException("educacao_transporte_aluno");
  if(r is null)throw new ArgumentException("Presença é obrigatória.");
  if(r.AlunoId<=0)throw new ArgumentException("Presença exige aluno.");
  var status=(r.Status ?? "").Trim().ToUpperInvariant();
  if(status is not ("PRESENTE" or "FALTANTE" or "DESLIGADO"))throw new TransportePresencaStatusInvalidoException(r.Status ?? "(nulo)");
  var corr=Guid.TryParse(x.CorrelationId,out var g)?g:Guid.NewGuid();
  using var c=(NpgsqlConnection)_context.CreateConnection();
  await c.OpenAsync(ct).ConfigureAwait(false);
  var v=await CarregarViagemNoEscopoAsync(x,viagemId,ct,c).ConfigureAwait(false);
  if(v.Status is ("CANCELADA" or "SUBSTITUIDA"))throw new TransporteViagemEstadoInvalidoException(viagemId,"LANCAR_PRESENCA",v.Status);
  var a=await c.ExecuteScalarAsync<long?>(new CommandDefinition("select coalesce(entidade_id,-1) from sigov.aluno where tenant_id=@TenantId and id=@Id and ativo and not is_deleted",new{TenantId=x.TenantId,Id=r.AlunoId},cancellationToken:ct)).ConfigureAwait(false);
  if(a is null)throw new TransporteAlunoInexistenteException(r.AlunoId);
  if(a.Value>=0&&x.EntidadeId.HasValue&&a.Value!=x.EntidadeId.Value)throw new ContextoInstitucionalDivergenteException("aluno",r.AlunoId,a.Value,x.EntidadeId.Value);
  var vinc=await c.ExecuteScalarAsync<bool>(new CommandDefinition("select exists(select 1 from sigov.educacao_transporte_aluno where tenant_id=@TenantId and coalesce(rota_id,(0)::bigint)=coalesce(@Rota,(0)::bigint) and coalesce(aluno_id,(0)::bigint)=coalesce(@Aluno,(0)::bigint) and ativo and is_deleted=false and status in ('ATIVA','ATIVO'))",new{TenantId=x.TenantId,Rota=v.RotaId,Aluno=r.AlunoId},cancellationToken:ct)).ConfigureAwait(false);
  if(!vinc)throw new TransportePresencaAlunoForaDaRotaException(r.AlunoId,v.RotaId ?? 0);
  await using var tx=await c.BeginTransactionAsync(ct).ConfigureAwait(false);
  var reg=await c.QueryFirstOrDefaultAsync<(long Id,string? Status)>(new CommandDefinition("select coalesce(id,(0)::bigint) as Id,coalesce(status,'') as Status from sigov.educacao_transporte_frequencia where tenant_id=@TenantId and coalesce(referencia_id,(0)::bigint)=coalesce(@Viagem,(0)::bigint) and coalesce(aluno_id,(0)::bigint)=coalesce(@Aluno,(0)::bigint) and tipo='PRESENCA_TRANSPORTE' and ativo and is_deleted=false order by id limit 1 for update",new{TenantId=x.TenantId,Viagem=viagemId,Aluno=r.AlunoId},tx,cancellationToken:ct)).ConfigureAwait(false);
  if(reg.Id>0){
    if(reg.Status==status){await tx.CommitAsync(ct).ConfigureAwait(false); return reg.Id;}
    if(reg.Status!="ESTORNADO"&&v.PresencaFechada)throw new TransportePresencasFechadasEstornoObrigatorioException(viagemId,r.AlunoId);
    var nu=await c.ExecuteAsync(new CommandDefinition("update sigov.educacao_transporte_frequencia set status=@Status,updated_at=now(),auditoria=(case jsonb_typeof(auditoria) when 'array' then auditoria else '[]'::jsonb end)||jsonb_build_array(jsonb_build_object('acao','RELANCAR_PRESENCA','de',@De,'em',now())) where tenant_id=@TenantId and id=@Id",new{Status=status,De=reg.Status,Id=reg.Id,TenantId=x.TenantId},tx,cancellationToken:ct)).ConfigureAwait(false);
    if(nu!=1)throw new ConflitoDeVersaoEducacaoException("educacao_transporte_frequencia",reg.Id);
    await tx.CommitAsync(ct).ConfigureAwait(false);
    return reg.Id;
  }
  long novo;
  try{
    novo=await c.ExecuteScalarAsync<long>(new CommandDefinition("insert into sigov.educacao_transporte_frequencia(tenant_id,entidade_id,escola_id,aluno_id,referencia_id,tipo,status,descricao,dados,auditoria,correlation_id,created_by) values(@TenantId,@EntidadeId,@EscolaId,@Aluno,@Viagem,'PRESENCA_TRANSPORTE',@Status,'PRESENCA_TRANSPORTE','{}'::jsonb,jsonb_build_array(jsonb_build_object('acao','LANCAR_PRESENCA','em',now())),@CorrelationId,@UsuarioId) returning id",new{TenantId=x.TenantId,EntidadeId=v.EntidadeId,EscolaId=v.EscolaId,Aluno=r.AlunoId,Viagem=viagemId,Status=status,CorrelationId=corr,UsuarioId=x.UsuarioId},tx,cancellationToken:ct)).ConfigureAwait(false);
  }catch(NpgsqlException npe){
    if(npe.SqlState=="23505"){
      // transacao abortada apos o 23505: diagnostico em conexao separada (evita 25P02 na tx abortada).
      using var c2=(NpgsqlConnection)_context.CreateConnection();
      await c2.OpenAsync(ct).ConfigureAwait(false);
      var ex2=await c2.QueryFirstOrDefaultAsync<(long Id,string? Status)>(new CommandDefinition("select coalesce(id,(0)::bigint) as Id,coalesce(status,'') as Status from sigov.educacao_transporte_frequencia where tenant_id=@TenantId and coalesce(referencia_id,(0)::bigint)=coalesce(@Viagem,(0)::bigint) and coalesce(aluno_id,(0)::bigint)=coalesce(@Aluno,(0)::bigint) and tipo='PRESENCA_TRANSPORTE' and ativo and is_deleted=false order by id limit 1",new{TenantId=x.TenantId,Viagem=viagemId,Aluno=r.AlunoId},cancellationToken:ct)).ConfigureAwait(false);
      if(ex2.Id>0&&ex2.Status==status){await tx.CommitAsync(ct).ConfigureAwait(false); return ex2.Id;}
      throw v.PresencaFechada?new TransportePresencasFechadasEstornoObrigatorioException(viagemId,r.AlunoId):new ConflitoDeVersaoEducacaoException("educacao_transporte_frequencia",ex2.Id);
    }
    throw;
  }
  await tx.CommitAsync(ct).ConfigureAwait(false);
  return novo;
}
public async Task<long> FecharPresencasAsync(EducacaoAvancadaContext x,long viagemId,CancellationToken ct){
  if(!await _inspector.TableExistsAsync("sigov","educacao_transporte_viagem",ct).ConfigureAwait(false))throw new RecursoNaoDisponivelException("educacao_transporte_viagem");
  using var c=(NpgsqlConnection)_context.CreateConnection();
  await c.OpenAsync(ct).ConfigureAwait(false);
  var v=await CarregarViagemNoEscopoAsync(x,viagemId,ct,c).ConfigureAwait(false);
  if(v.Status is ("CANCELADA" or "SUBSTITUIDA"))throw new TransporteViagemEstadoInvalidoException(viagemId,"FECHAR_PRESENCAS",v.Status);
  if(v.PresencaFechada)throw new TransportePresencasJaFechadasException(viagemId);
  var vinculados=await c.ExecuteScalarAsync<int>(new CommandDefinition("select count(1) from sigov.educacao_transporte_aluno where tenant_id=@TenantId and coalesce(rota_id,(0)::bigint)=coalesce(@Rota,(0)::bigint) and ativo and is_deleted=false and status in ('ATIVA','ATIVO')",new{TenantId=x.TenantId,Rota=v.RotaId},cancellationToken:ct)).ConfigureAwait(false);
  var registrados=await c.ExecuteScalarAsync<int>(new CommandDefinition("select count(distinct aluno_id) from sigov.educacao_transporte_frequencia where tenant_id=@TenantId and coalesce(referencia_id,(0)::bigint)=coalesce(@Viagem,(0)::bigint) and tipo='PRESENCA_TRANSPORTE' and ativo and is_deleted=false and status<>'ESTORNADO'",new{TenantId=x.TenantId,Viagem=viagemId},cancellationToken:ct)).ConfigureAwait(false);
  if(registrados<vinculados)throw new TransportePresencasIncompletasException(viagemId,vinculados,registrados);
  var n=await c.ExecuteAsync(new CommandDefinition("update sigov.educacao_transporte_viagem set presenca_fechada=true,updated_at=now(),updated_by=@UsuarioId,auditoria=(case jsonb_typeof(auditoria) when 'array' then auditoria else '[]'::jsonb end)||jsonb_build_array(jsonb_build_object('acao','FECHAR_PRESENCAS','em',now())) where tenant_id=@TenantId and id=@Id",new{UsuarioId=x.UsuarioId,TenantId=x.TenantId,Id=viagemId},cancellationToken:ct)).ConfigureAwait(false);
  if(n!=1)throw new ConflitoDeVersaoEducacaoException("educacao_transporte_viagem",viagemId);
  return viagemId;
}
public async Task<long> EstornarPresencaAsync(EducacaoAvancadaContext x,long viagemId,long presencaId,EducacaoTransporteEstornarPresencaRequest r,CancellationToken ct){
  if(!await _inspector.TableExistsAsync("sigov","educacao_transporte_frequencia",ct).ConfigureAwait(false))throw new RecursoNaoDisponivelException("educacao_transporte_frequencia");
  if(r is null)throw new ArgumentException("Estorno é obrigatório.");
  var just=(r.Justificativa ?? "").Trim();
  if(just.Length==0)throw new TransportePresencaEstornoJustificativaObrigatorioException();
  using var c=(NpgsqlConnection)_context.CreateConnection();
  await c.OpenAsync(ct).ConfigureAwait(false);
  var reg=await c.QueryFirstOrDefaultAsync<(long Id,string? Status)>(new CommandDefinition("select coalesce(id,(0)::bigint) as Id,coalesce(status,'') as Status from sigov.educacao_transporte_frequencia where tenant_id=@TenantId and id=@Id and coalesce(referencia_id,(0)::bigint)=coalesce(@Viagem,(0)::bigint) and tipo='PRESENCA_TRANSPORTE' and ativo and is_deleted=false",new{TenantId=x.TenantId,Id=presencaId,Viagem=viagemId},cancellationToken:ct)).ConfigureAwait(false);
  if(reg.Id==0)throw new TransportePresencaInexistenteException(presencaId);
  if(reg.Status=="ESTORNADO")return reg.Id; // compensacao unica: repeticao retorna o mesmo registro
  await using var tx=await c.BeginTransactionAsync(ct).ConfigureAwait(false);
  var n=await c.ExecuteAsync(new CommandDefinition("update sigov.educacao_transporte_frequencia set status='ESTORNADO',justificativa=@Just,updated_at=now(),updated_by=@UsuarioId,auditoria=(case jsonb_typeof(auditoria) when 'array' then auditoria else '[]'::jsonb end)||jsonb_build_array(jsonb_build_object('acao','ESTORNAR_PRESENCA','de',@De,'em',now(),'justificativa',@Just)) where tenant_id=@TenantId and id=@Id and status<>'ESTORNADO'",new{Just=just,De=reg.Status,UsuarioId=x.UsuarioId,TenantId=x.TenantId,Id=presencaId},tx,cancellationToken:ct)).ConfigureAwait(false);
  if(n==0)return reg.Id;
  if(n!=1)throw new ConflitoDeVersaoEducacaoException("educacao_transporte_frequencia",presencaId);
  await tx.CommitAsync(ct).ConfigureAwait(false);
  return reg.Id;
}
public async Task<long> LancarOcorrenciaViagemAsync(EducacaoAvancadaContext x,long viagemId,EducacaoTransporteLancarOcorrenciaViagemRequest r,CancellationToken ct){
  if(!await _inspector.TableExistsAsync("sigov","educacao_transporte_ocorrencia",ct).ConfigureAwait(false))throw new RecursoNaoDisponivelException("educacao_transporte_ocorrencia");
  if(r is null)throw new ArgumentException("Ocorrência é obrigatória.");
  var tipo=(r.Tipo ?? "").Trim().ToUpperInvariant();
  if(tipo is not ("ACIDENTE" or "QUEBRA" or "CLIMA" or "ROTINEIRA_OUTRA"))throw new TransporteOcorrenciaTipoInvalidoException(r.Tipo ?? "(nulo)");
  var grav=(r.Gravidade ?? "").Trim().ToUpperInvariant();
  if(grav is not ("BAIXA" or "MEDIA" or "ALTA"))throw new TransporteOcorrenciaGravidadeInvalidaException(r.Gravidade ?? "(nulo)");
  var desc=(r.Descricao ?? "").Trim();
  if(desc.Length==0)throw new TransporteOcorrenciaDescricaoObrigatorioException();
  var corr=Guid.TryParse(x.CorrelationId,out var g)?g:Guid.NewGuid();
  using var c=(NpgsqlConnection)_context.CreateConnection();
  await c.OpenAsync(ct).ConfigureAwait(false);
  var v=await CarregarViagemNoEscopoAsync(x,viagemId,ct,c).ConfigureAwait(false);
  if(v.Status is ("CONCLUIDA" or "CANCELADA" or "SUBSTITUIDA"))throw new TransporteViagemEstadoInvalidoException(viagemId,"LANCAR_OCORRENCIA",v.Status);
  var quando=r.DataHora.HasValue?ComoUtc(r.DataHora.Value):_clock.UtcNow.DateTime;
  await using var tx=await c.BeginTransactionAsync(ct).ConfigureAwait(false);
  var graveVigente=await c.ExecuteScalarAsync<long>(new CommandDefinition("select coalesce(min(id),(0)::bigint) from sigov.educacao_transporte_ocorrencia where tenant_id=@TenantId and coalesce(referencia_id,(0)::bigint)=coalesce(@Viagem,(0)::bigint) and gravidade='ALTA' and desfecho_em is null and ativo and not is_deleted",new{TenantId=x.TenantId,Viagem=viagemId},tx,cancellationToken:ct)).ConfigureAwait(false);
  if(grav=="ALTA"&&graveVigente>0)throw new TransporteOcorrenciaAltaVigenteException(graveVigente);
  long oid;
  try{
    oid=await c.ExecuteScalarAsync<long>(new CommandDefinition("insert into sigov.educacao_transporte_ocorrencia(tenant_id,entidade_id,escola_id,referencia_id,veiculo_id,nome,tipo,gravidade,descricao,competencia,data_hora,status,dados,auditoria,correlation_id,created_by) values(@TenantId,@EntidadeId,@EscolaId,@Viagem,@Veiculo,@Nome,@Tipo,@Gravidade,@Descricao,@Data,@Quando,'ATIVA','{}'::jsonb,jsonb_build_array(jsonb_build_object('acao','LANCAR_OCORRENCIA_VIAGEM','em',now())),@CorrelationId,@UsuarioId) returning id",new{TenantId=x.TenantId,EntidadeId=v.EntidadeId,EscolaId=v.EscolaId,Viagem=viagemId,Veiculo=v.VeiculoId,Nome=tipo,Tipo=tipo,Gravidade=grav,Descricao=desc,Data=v.DataInicio??_clock.DataAtual,Quando=quando,CorrelationId=corr,UsuarioId=x.UsuarioId},tx,cancellationToken:ct)).ConfigureAwait(false);
  }catch(NpgsqlException npe){
    if(npe.SqlState=="23505")throw new TransporteOcorrenciaAltaVigenteException(graveVigente>0?graveVigente:-1);
    throw;
  }
  if(grav=="ALTA"){
    // BR-4.5: ALTA muda a situacao da viagem para OCORRENCIA_GRAVE; o status anterior vai ao
    // dados da ocorrencia (fonte do DesfecharOcorrenciaViagemAsync na restauracao pos-desfecho)
    await c.ExecuteAsync(new CommandDefinition("update sigov.educacao_transporte_ocorrencia set dados=(case jsonb_typeof(dados) when 'object' then dados else '{}'::jsonb end)||jsonb_build_object('status_anterior',@StatusAnterior) where tenant_id=@TenantId and id=@Oc",new{StatusAnterior=v.Status ?? "PROGRAMADA",TenantId=x.TenantId,Oc=oid},tx,cancellationToken:ct)).ConfigureAwait(false);
    await c.ExecuteAsync(new CommandDefinition("update sigov.educacao_transporte_viagem set status='OCORRENCIA_GRAVE',updated_at=now(),updated_by=@UsuarioId,auditoria=(case jsonb_typeof(auditoria) when 'array' then auditoria else '[]'::jsonb end)||jsonb_build_array(jsonb_build_object('acao','OCORRENCIA_GRAVE','ocorrenciaId',@Oc)) where tenant_id=@TenantId and id=@Id",new{Oc=oid,UsuarioId=x.UsuarioId,TenantId=x.TenantId,Id=viagemId},tx,cancellationToken:ct)).ConfigureAwait(false);
  }
  await tx.CommitAsync(ct).ConfigureAwait(false);
  return oid;
}
public async Task<long> DesfecharOcorrenciaViagemAsync(EducacaoAvancadaContext x,long viagemId,long ocorrenciaId,EducacaoTransporteDesfecharOcorrenciaRequest r,CancellationToken ct){
  if(!await _inspector.TableExistsAsync("sigov","educacao_transporte_ocorrencia",ct).ConfigureAwait(false))throw new RecursoNaoDisponivelException("educacao_transporte_ocorrencia");
  if(r is null)throw new ArgumentException("Desfecho é obrigatório.");
  var rel=(r.Relatorio ?? "").Trim();
  if(rel.Length==0)throw new TransporteOcorrenciaDesfechoRelatorioObrigatorioException();
  using var c=(NpgsqlConnection)_context.CreateConnection();
  await c.OpenAsync(ct).ConfigureAwait(false);
  var o=await c.QueryFirstOrDefaultAsync<(long Id,DateTime? DesfechoEm,string? StatusAnterior)>(new CommandDefinition("select coalesce(id,(0)::bigint) as Id,desfecho_em as DesfechoEm,coalesce(dados->>'status_anterior','') as StatusAnterior from sigov.educacao_transporte_ocorrencia where tenant_id=@TenantId and id=@Id and coalesce(referencia_id,(0)::bigint)=coalesce(@Viagem,(0)::bigint) and ativo and not is_deleted",new{TenantId=x.TenantId,Id=ocorrenciaId,Viagem=viagemId},cancellationToken:ct)).ConfigureAwait(false);
  if(o.Id==0)throw new TransporteOcorrenciaInexistenteException(ocorrenciaId);
  if(o.DesfechoEm is not null)return o.Id; // desfecho unico (repeticao idempotente)
  var v=await CarregarViagemNoEscopoAsync(x,viagemId,ct,c).ConfigureAwait(false);
  await using var tx=await c.BeginTransactionAsync(ct).ConfigureAwait(false);
  var n=await c.ExecuteAsync(new CommandDefinition("update sigov.educacao_transporte_ocorrencia set desfecho_relatorio=@Rel,desfecho_em=now(),updated_at=now(),updated_by=@UsuarioId,auditoria=(case jsonb_typeof(auditoria) when 'array' then auditoria else '[]'::jsonb end)||jsonb_build_array(jsonb_build_object('acao','DESFECHAR_OCORRENCIA','em',now())) where tenant_id=@TenantId and id=@Id and desfecho_em is null",new{Rel=rel,UsuarioId=x.UsuarioId,TenantId=x.TenantId,Id=ocorrenciaId},tx,cancellationToken:ct)).ConfigureAwait(false);
  if(n==0)throw new TransporteOcorrenciaJaDesfechadaException(ocorrenciaId);
  if(n!=1)throw new ConflitoDeVersaoEducacaoException("educacao_transporte_ocorrencia",ocorrenciaId);
  if(v.Status=="OCORRENCIA_GRAVE"){
    var anterior=string.IsNullOrWhiteSpace(o.StatusAnterior)?"PROGRAMADA":o.StatusAnterior.Trim();
    await c.ExecuteAsync(new CommandDefinition("update sigov.educacao_transporte_viagem set status=@Anterior,updated_at=now(),updated_by=@UsuarioId,auditoria=(case jsonb_typeof(auditoria) when 'array' then auditoria else '[]'::jsonb end)||jsonb_build_array(jsonb_build_object('acao','RESTAURAR_STATUS_POS_DESFECHO','ocorrenciaId',@Oc)) where tenant_id=@TenantId and id=@Id and status='OCORRENCIA_GRAVE'",new{Anterior=anterior,Oc=ocorrenciaId,UsuarioId=x.UsuarioId,TenantId=x.TenantId,Id=viagemId},tx,cancellationToken:ct)).ConfigureAwait(false);
  }
  await tx.CommitAsync(ct).ConfigureAwait(false);
  return o.Id;
}
public async Task<long> RegistrarCustoRealizadoAsync(EducacaoAvancadaContext x,long viagemId,EducacaoTransporteRegistrarCustoRealizadoRequest r,CancellationToken ct){
  if(!await _inspector.TableExistsAsync("sigov","educacao_transporte_custo",ct).ConfigureAwait(false))throw new RecursoNaoDisponivelException("educacao_transporte_custo");
  if(r is null)throw new ArgumentException("Custo realizado é obrigatório.");
  if(r.Valor<=0m)throw new TransporteCustoRealizadoValorInvalidoException(r.Valor);
  var corr=Guid.TryParse(x.CorrelationId,out var g)?g:Guid.NewGuid();
  using var c=(NpgsqlConnection)_context.CreateConnection();
  await c.OpenAsync(ct).ConfigureAwait(false);
  var v=await CarregarViagemNoEscopoAsync(x,viagemId,ct,c).ConfigureAwait(false);
  if(v.Status is ("CANCELADA" or "SUBSTITUIDA"))throw new TransporteViagemEstadoInvalidoException(viagemId,"REGISTRAR_CUSTO_REALIZADO",v.Status);
  var doc=(r.Documento ?? "").Trim();
  try{
    return await c.ExecuteScalarAsync<long>(new CommandDefinition("insert into sigov.educacao_transporte_custo(tenant_id,entidade_id,escola_id,referencia_id,rota_id,veiculo_id,motorista_id,competencia,tipo,valor,descricao,dados,auditoria,correlation_id,created_by) values(@TenantId,@EntidadeId,@EscolaId,@Viagem,@Rota,@Veiculo,@Motorista,@Data,'REALIZADO',@Valor,@Desc,'{}'::jsonb,jsonb_build_array(jsonb_build_object('acao','REGISTRAR_CUSTO_REALIZADO','em',now())),@CorrelationId,@UsuarioId) returning id",new{TenantId=x.TenantId,EntidadeId=v.EntidadeId,EscolaId=v.EscolaId,Viagem=viagemId,Rota=v.RotaId,Veiculo=v.VeiculoId,Motorista=v.MotoristaId,Data=v.DataInicio??_clock.DataAtual,Valor=r.Valor,Desc=doc.Length>0?doc:"CUSTO_EFETIVO",CorrelationId=corr,UsuarioId=x.UsuarioId},cancellationToken:ct)).ConfigureAwait(false);
  }catch(NpgsqlException npe){
    if(npe.SqlState=="23505")throw new TransporteCustoRealizadoExistenteException(viagemId);
    throw;
  }
}
public async Task<EducacaoTransporteRelatorioViagemDto> RelatorioViagemAsync(EducacaoAvancadaContext x,long viagemId,CancellationToken ct){
  foreach(var t in new[]{"educacao_transporte_viagem","educacao_transporte_frequencia","educacao_transporte_ocorrencia","educacao_transporte_custo"})if(!await _inspector.TableExistsAsync("sigov",t,ct).ConfigureAwait(false))throw new RecursoNaoDisponivelException(t);
  using var c=(NpgsqlConnection)_context.CreateConnection();
  await c.OpenAsync(ct).ConfigureAwait(false);
  var v=await CarregarViagemNoEscopoAsync(x,viagemId,ct,c).ConfigureAwait(false);
  var p=new{TenantId=x.TenantId,Viagem=viagemId,Rota=v.RotaId};
  var alunos=await c.QueryAsync<EducacaoTransporteRelatorioViagemAlunoDto>(new CommandDefinition("select a.id as AlunoId,coalesce(nullif(p.nome,''),'SEM_NOME') as Nome,coalesce(f.status,'SEM_REGISTRO') as Status from sigov.educacao_transporte_aluno ta join sigov.aluno a on a.id=ta.aluno_id left join sigov.pessoa p on p.id=a.pessoa_id left join (select distinct on (aluno_id) aluno_id,status from sigov.educacao_transporte_frequencia where tenant_id=@TenantId and coalesce(referencia_id,(0)::bigint)=coalesce(@Viagem,(0)::bigint) and tipo='PRESENCA_TRANSPORTE' and ativo and not is_deleted and status<>'ESTORNADO' order by aluno_id,id desc) f on f.aluno_id=ta.aluno_id where ta.tenant_id=@TenantId and coalesce(ta.rota_id,(0)::bigint)=coalesce(@Rota,(0)::bigint) and ta.ativo and not ta.is_deleted and ta.status in ('ATIVA','ATIVO') order by a.id",p,cancellationToken:ct)).ConfigureAwait(false);
  var ocorrencias=await c.QueryAsync<EducacaoTransporteRelatorioViagemOcorrenciaDto>(new CommandDefinition("select id as Id,coalesce(tipo,'') as Tipo,coalesce(gravidade,'') as Gravidade,coalesce(descricao,'') as Descricao,data_hora as DataHora,desfecho_em as DesfechoEm from sigov.educacao_transporte_ocorrencia where tenant_id=@TenantId and coalesce(referencia_id,(0)::bigint)=coalesce(@Viagem,(0)::bigint) and ativo and not is_deleted order by id limit 50",p,cancellationToken:ct)).ConfigureAwait(false);
  var custos=await c.QuerySingleAsync<(decimal Estimado,decimal? Realizado)>(new CommandDefinition("select coalesce(max(case when tipo='ESTIMADO' then valor end),0) as Estimado,max(case when tipo='REALIZADO' then valor end) as Realizado from sigov.educacao_transporte_custo where tenant_id=@TenantId and coalesce(referencia_id,(0)::bigint)=coalesce(@Viagem,(0)::bigint) and ativo and not is_deleted",p,cancellationToken:ct)).ConfigureAwait(false);
  decimal? desvioAbs=custos.Realizado.HasValue?Math.Abs(custos.Realizado.Value-custos.Estimado):null;
  decimal? desvioPct=custos.Realizado.HasValue&&custos.Estimado>0m?Math.Round(Math.Abs(custos.Realizado.Value-custos.Estimado)/custos.Estimado*100m,2):null;
  return new EducacaoTransporteRelatorioViagemDto(viagemId,v.DataInicio??_clock.DataAtual,v.RotaId??0,v.VeiculoId??0,v.MotoristaId??0,v.Versao,v.Status ?? "INDEFINIDO",alunos.Count(),alunos.Count(a=>a.Status=="PRESENTE"),alunos.Count(a=>a.Status=="FALTANTE"),alunos.Count(a=>a.Status=="DESLIGADO"),v.KmEstimado,null,custos.Estimado,custos.Realizado,desvioAbs,desvioPct,alunos.ToList(),ocorrencias.ToList(),"origem verificavel: sigov.educacao_transporte_viagem (ciclo/km/versao) + _frequencia tipo=PRESENCA_TRANSPORTE (presenca) + _ocorrencia (tipo/gravidade/desfecho) + _custo tipo in (ESTIMADO,REALIZADO) comparado por viagem; filtro tenant_id="+x.TenantId+" e referencia_id/rota da viagem "+viagemId+"; desvio = |realizado - estimado| em absoluto e % sobre o estimado",_clock.UtcNow.DateTime);
}
  public async Task<EducacaoTransporteDashboardDto> TransporteDashboardAsync(long tenantId,CancellationToken ct){
    foreach(var t in new[]{"educacao_transporte_rota","educacao_transporte_rota_ponto","educacao_transporte_aluno"})if(!await _inspector.TableExistsAsync("sigov",t,ct).ConfigureAwait(false))throw new RecursoNaoDisponivelException(t);
   using var c=_context.CreateConnection();
   var sql="select (select count(1) from sigov.educacao_transporte_rota r where r.tenant_id=@TenantId and r.ativo and not r.is_deleted and r.status in ('ATIVA','ATIVO')) as RotasAtivas,(select count(1) from sigov.educacao_transporte_rota_ponto p where p.tenant_id=@TenantId and p.ativo and not p.is_deleted and p.status in ('ATIVA','ATIVO')) as PontosAtivos,(select count(1) from sigov.educacao_transporte_aluno a where a.tenant_id=@TenantId and a.ativo and not a.is_deleted and a.status in ('ATIVA','ATIVO')) as AlunosVinculados,(select count(distinct r.escola_id) from sigov.educacao_transporte_rota r where r.tenant_id=@TenantId and r.ativo and not r.is_deleted and r.status in ('ATIVA','ATIVO') and r.escola_id is not null) as EscolasAtendidas";
   var d=await c.QueryFirstAsync<EducacaoTransporteDashboardDto>(new CommandDefinition(sql,new{TenantId=tenantId},cancellationToken:ct)).ConfigureAwait(false);
   return d;
 }
  private sealed class EducacaoCentroCustoLinha{public long Id{get;set;} public long? EntidadeId{get;set;} public string? Status{get;set;}}
  private sealed class EducacaoIndicadoresMetricasLinha{public decimal Receitas{get;set;} public decimal Aplicacoes{get;set;} public decimal Custos{get;set;} public long Matriculas{get;set;} public long Inconsistencias{get;set;} public decimal? TaxaMinima{get;set;}}
  private sealed class EducacaoIndicadorLinha{public long Id{get;set;} public long? EntidadeId{get;set;} public string? Status{get;set;}}
  private sealed class EducacaoEducacensoLoteLinha{public long Id{get;set;} public long? EntidadeId{get;set;} public string? Status{get;set;}}
  private sealed class EducacaoEducacensoMetricasLinha{public long LotesAtivos{get;set;} public long InconsistenciasAbertas{get;set;} public long GravidadeAlta{get;set;} public long Eventos{get;set;}}
  async Task<EducacaoCentroCustoLinha> CarregarCentroCustoNoEscopoAsync(EducacaoAvancadaContext x,long centroId,CancellationToken ct,NpgsqlConnection c){
    var r=await c.QueryFirstOrDefaultAsync<EducacaoCentroCustoLinha>(new CommandDefinition("select id as Id,entidade_id as EntidadeId,status as Status from sigov.educacao_custo_centro where tenant_id=@TenantId and id=@Id and ativo and not is_deleted",new{TenantId=x.TenantId,Id=centroId},cancellationToken:ct)).ConfigureAwait(false);
    if(r is null)throw new CustoCentroInexistenteException(centroId);
    if(r.EntidadeId>=0&&x.EntidadeId.HasValue&&r.EntidadeId.Value!=x.EntidadeId.Value)throw new ContextoInstitucionalDivergenteException("educacao_custo_centro",centroId,r.EntidadeId.Value,x.EntidadeId.Value);
    return r;
  }
  public async Task<long> LancarParametroAsync(EducacaoAvancadaContext x,EducacaoFundebLancarParametroRequest r,CancellationToken ct){
    if(!await _inspector.TableExistsAsync("sigov","educacao_fundeb_parametro",ct).ConfigureAwait(false))throw new RecursoNaoDisponivelException("educacao_fundeb_parametro");
    if(r is null)throw new ArgumentException("Parâmetro FDEB é obrigatório.");
    if(string.IsNullOrWhiteSpace(r.Codigo))throw new ArgumentException("Parâmetro FDEB exige código.");
    if(r.Valor<0)throw new ArgumentException("Valor do parâmetro FDEB não pode ser negativo.");
    var codigo=r.Codigo.Trim().ToUpperInvariant();
    var corr=Guid.TryParse(x.CorrelationId,out var g)?g:Guid.NewGuid();
    using var c=(NpgsqlConnection)_context.CreateConnection();
    await c.OpenAsync(ct).ConfigureAwait(false);
    await using var tx=await c.BeginTransactionAsync(ct).ConfigureAwait(false);
    var existe=await c.ExecuteScalarAsync<bool>(new CommandDefinition("select exists(select 1 from sigov.educacao_fundeb_parametro where tenant_id=@TenantId and upper(coalesce(codigo,''))=@Codigo and ativo and is_deleted=false and status not in ('ENCERRADA','ENCERRADO','CANCELADA','CANCELADO'))",new{TenantId=x.TenantId,Codigo=codigo},tx,cancellationToken:ct)).ConfigureAwait(false);
    if(existe)throw new FundebParametroCodigoExistenteException(codigo);
    long id;
    try{
      id=await c.ExecuteScalarAsync<long>(new CommandDefinition("insert into sigov.educacao_fundeb_parametro(tenant_id,entidade_id,codigo,nome,tipo,valor,status,auditoria,correlation_id,created_by) values(@TenantId,@EntidadeId,@Codigo,@Codigo,'FDEB_PARAMETRO',@Valor,'ATIVA',jsonb_build_array(jsonb_build_object('acao','LANCAR_PARAMETRO','em',now())),@CorrelationId,@UsuarioId) returning id",new{TenantId=x.TenantId,EntidadeId=x.EntidadeId,Codigo=codigo,Valor=r.Valor,CorrelationId=corr,UsuarioId=x.UsuarioId},tx,cancellationToken:ct)).ConfigureAwait(false);
    }catch(NpgsqlException npe){
      if(npe.SqlState=="23505")throw new FundebParametroCodigoExistenteException(codigo);
      throw;
    }
    await tx.CommitAsync(ct).ConfigureAwait(false);
    return id;
  }
  public async Task<long> LancarReceitaAsync(EducacaoAvancadaContext x,EducacaoFundebLancarReceitaRequest r,CancellationToken ct){
    if(!await _inspector.TableExistsAsync("sigov","educacao_fundeb_receita",ct).ConfigureAwait(false))throw new RecursoNaoDisponivelException("educacao_fundeb_receita");
    if(r is null)throw new ArgumentException("Receita FDEB é obrigatória.");
    if(r.Competencia<=new DateOnly(2000,1,1))throw new ArgumentException("Receita FDEB exige competência válida (data).");
    if(string.IsNullOrWhiteSpace(r.Fonte))throw new ArgumentException("Receita FDEB exige fonte (origem do recurso, ex.: FPE, FNDE, receita própria).");
    if(r.Valor<=0)throw new ArgumentException("Valor da receita FDEB deve ser positivo.");
    var fonte=r.Fonte.Trim().ToUpperInvariant();
    var corr=Guid.TryParse(x.CorrelationId,out var g)?g:Guid.NewGuid();
    using var c=(NpgsqlConnection)_context.CreateConnection();
    await c.OpenAsync(ct).ConfigureAwait(false);
    await using var tx=await c.BeginTransactionAsync(ct).ConfigureAwait(false);
    var existe=await c.ExecuteScalarAsync<bool>(new CommandDefinition("select exists(select 1 from sigov.educacao_fundeb_receita where tenant_id=@TenantId and competencia=@Competencia and upper(coalesce(tipo,''))=@Fonte and ativo and is_deleted=false and status not in ('ENCERRADA','ENCERRADO','CANCELADA','CANCELADO'))",new{TenantId=x.TenantId,Competencia=r.Competencia,Fonte=fonte},tx,cancellationToken:ct)).ConfigureAwait(false);
    if(existe)throw new FundebReceitaDuplicadaException(r.Competencia.ToString("yyyy-MM-dd"),fonte);
    long id;
    try{
      id=await c.ExecuteScalarAsync<long>(new CommandDefinition("insert into sigov.educacao_fundeb_receita(tenant_id,entidade_id,competencia,tipo,valor,status,auditoria,correlation_id,created_by) values(@TenantId,@EntidadeId,@Competencia,@Fonte,@Valor,'ATIVA',jsonb_build_array(jsonb_build_object('acao','LANCAR_RECEITA','em',now())),@CorrelationId,@UsuarioId) returning id",new{TenantId=x.TenantId,EntidadeId=x.EntidadeId,Competencia=r.Competencia,Fonte=fonte,Valor=r.Valor,CorrelationId=corr,UsuarioId=x.UsuarioId},tx,cancellationToken:ct)).ConfigureAwait(false);
    }catch(NpgsqlException npe){
      if(npe.SqlState=="23505")throw new FundebReceitaDuplicadaException(r.Competencia.ToString("yyyy-MM-dd"),fonte);
      throw;
    }
    await tx.CommitAsync(ct).ConfigureAwait(false);
    return id;
  }
  public async Task<long> LancarAplicacaoAsync(EducacaoAvancadaContext x,EducacaoFundebLancarAplicacaoRequest r,CancellationToken ct){
    if(!await _inspector.TableExistsAsync("sigov","educacao_fundeb_aplicacao",ct).ConfigureAwait(false))throw new RecursoNaoDisponivelException("educacao_fundeb_aplicacao");
    if(!await _inspector.TableExistsAsync("sigov","educacao_custo_centro",ct).ConfigureAwait(false))throw new RecursoNaoDisponivelException("educacao_custo_centro");
    if(r is null)throw new ArgumentException("Aplicação FDEB é obrigatória.");
    if(r.CentroCustoId<=0)throw new ArgumentException("Aplicação FDEB exige centro de custo.");
    if(r.Valor<=0)throw new ArgumentException("Valor da aplicação FDEB deve ser positivo.");
    var corr=Guid.TryParse(x.CorrelationId,out var g)?g:Guid.NewGuid();
    using var c=(NpgsqlConnection)_context.CreateConnection();
    await c.OpenAsync(ct).ConfigureAwait(false);
    var centro=await CarregarCentroCustoNoEscopoAsync(x,r.CentroCustoId,ct,c).ConfigureAwait(false);
    if(centro.Status is not ("ATIVA" or "ATIVO"))throw new CustoCentroNaoVigenteException(r.CentroCustoId,centro.Status ?? "INDEFINIDO");
    return await c.ExecuteScalarAsync<long>(new CommandDefinition("insert into sigov.educacao_fundeb_aplicacao(tenant_id,entidade_id,centro_custo_id,valor,status,auditoria,correlation_id,created_by) values(@TenantId,@EntidadeId,@CentroCustoId,@Valor,'ATIVA',jsonb_build_array(jsonb_build_object('acao','LANCAR_APLICACAO','em',now())),@CorrelationId,@UsuarioId) returning id",new{TenantId=x.TenantId,EntidadeId=x.EntidadeId,CentroCustoId=r.CentroCustoId,Valor=r.Valor,CorrelationId=corr,UsuarioId=x.UsuarioId},cancellationToken:ct)).ConfigureAwait(false);
  }
  public async Task<long> LancarCentroCustoAsync(EducacaoAvancadaContext x,EducacaoCustoLancarCentroRequest r,CancellationToken ct){
    if(!await _inspector.TableExistsAsync("sigov","educacao_custo_centro",ct).ConfigureAwait(false))throw new RecursoNaoDisponivelException("educacao_custo_centro");
    if(r is null)throw new ArgumentException("Centro de custo é obrigatório.");
    if(string.IsNullOrWhiteSpace(r.Codigo))throw new ArgumentException("Centro de custo exige código.");
    if(string.IsNullOrWhiteSpace(r.Nome))throw new ArgumentException("Centro de custo exige nome.");
    var codigo=r.Codigo.Trim().ToUpperInvariant();
    var corr=Guid.TryParse(x.CorrelationId,out var g)?g:Guid.NewGuid();
    using var c=(NpgsqlConnection)_context.CreateConnection();
    await c.OpenAsync(ct).ConfigureAwait(false);
    await using var tx=await c.BeginTransactionAsync(ct).ConfigureAwait(false);
    var existe=await c.ExecuteScalarAsync<bool>(new CommandDefinition("select exists(select 1 from sigov.educacao_custo_centro where tenant_id=@TenantId and upper(coalesce(codigo,''))=@Codigo and ativo and is_deleted=false and status not in ('ENCERRADA','ENCERRADO','CANCELADA','CANCELADO'))",new{TenantId=x.TenantId,Codigo=codigo},tx,cancellationToken:ct)).ConfigureAwait(false);
    if(existe)throw new CustoCentroCodigoExistenteException(codigo);
    long id;
    try{
      id=await c.ExecuteScalarAsync<long>(new CommandDefinition("insert into sigov.educacao_custo_centro(tenant_id,entidade_id,codigo,nome,tipo,status,auditoria,correlation_id,created_by) values(@TenantId,@EntidadeId,@Codigo,@Nome,'CUSTO_EDUCACAO','ATIVA',jsonb_build_array(jsonb_build_object('acao','LANCAR_CENTRO_CUSTO','em',now())),@CorrelationId,@UsuarioId) returning id",new{TenantId=x.TenantId,EntidadeId=x.EntidadeId,Codigo=codigo,Nome=r.Nome.Trim(),CorrelationId=corr,UsuarioId=x.UsuarioId},tx,cancellationToken:ct)).ConfigureAwait(false);
    }catch(NpgsqlException npe){
      if(npe.SqlState=="23505")throw new CustoCentroCodigoExistenteException(codigo);
      throw;
    }
    await tx.CommitAsync(ct).ConfigureAwait(false);
    return id;
  }
  public async Task<EducacaoIndicadoresDashboardDto> IndicadoresDashboardAsync(long tenantId,CancellationToken ct){
    foreach(var t in new[]{"educacao_fundeb_receita","educacao_fundeb_aplicacao","educacao_custo_lancamento","educacao_educacenso_inconsistencia","matricula"})if(!await _inspector.TableExistsAsync("sigov",t,ct).ConfigureAwait(false))throw new RecursoNaoDisponivelException(t);
    using var c=_context.CreateConnection();
    var sql="select (select coalesce(sum(valor),0) from sigov.educacao_fundeb_receita where tenant_id=@TenantId and ativo and not is_deleted and status not in ('ENCERRADA','ENCERRADO','CANCELADA','CANCELADO')) as Receitas,(select coalesce(sum(valor),0) from sigov.educacao_fundeb_aplicacao where tenant_id=@TenantId and ativo and not is_deleted and status not in ('ENCERRADA','ENCERRADO','CANCELADA','CANCELADO')) as Aplicacoes,(select coalesce(sum(valor),0) from sigov.educacao_custo_lancamento where tenant_id=@TenantId and ativo and not is_deleted and status not in ('ENCERRADA','ENCERRADO','CANCELADA','CANCELADO')) as Custos,(select count(1) from sigov.matricula where tenant_id=@TenantId and ativo and not is_deleted and status in ('ATIVA','CONFIRMADA')) as Matriculas,(select count(1) from sigov.educacao_educacenso_inconsistencia where tenant_id=@TenantId and ativo and not is_deleted and status not in ('ENCERRADA','ENCERRADO','CANCELADA','CANCELADO')) as Inconsistencias,(select valor from sigov.educacao_fundeb_parametro where tenant_id=@TenantId and ativo and not is_deleted and upper(coalesce(codigo,''))='TAXA_MINIMA_APLICACAO' limit 1) as TaxaMinima";
    var m=await c.QueryFirstAsync<EducacaoIndicadoresMetricasLinha>(new CommandDefinition(sql,new{TenantId=tenantId},cancellationToken:ct)).ConfigureAwait(false);
    var taxa=m.Receitas>0?Math.Round(m.Aplicacoes/m.Receitas,4):0m;
    var conformidade=m.TaxaMinima is >0&&m.Receitas>0&&taxa>=m.TaxaMinima.Value;
    return new EducacaoIndicadoresDashboardDto(m.Receitas,m.Aplicacoes,taxa,m.TaxaMinima,conformidade,m.Matriculas>0?Math.Round(m.Custos/m.Matriculas,2):0m,m.Inconsistencias);
  }
  public async Task<long> LancarIndicadorAsync(EducacaoAvancadaContext x,EducacaoIndicadorLancarRequest r,CancellationToken ct){
    if(!await _inspector.TableExistsAsync("sigov","educacao_indicador",ct).ConfigureAwait(false))throw new RecursoNaoDisponivelException("educacao_indicador");
    if(r is null)throw new ArgumentException("Indicador é obrigatório.");
    if(string.IsNullOrWhiteSpace(r.Codigo))throw new ArgumentException("Indicador exige código.");
    if(string.IsNullOrWhiteSpace(r.Nome))throw new ArgumentException("Indicador exige nome.");
    var codigo=r.Codigo.Trim().ToUpperInvariant();
    var corr=Guid.TryParse(x.CorrelationId,out var g)?g:Guid.NewGuid();
    using var c=(NpgsqlConnection)_context.CreateConnection();
    await c.OpenAsync(ct).ConfigureAwait(false);
    await using var tx=await c.BeginTransactionAsync(ct).ConfigureAwait(false);
    var existe=await c.ExecuteScalarAsync<bool>(new CommandDefinition("select exists(select 1 from sigov.educacao_indicador where tenant_id=@TenantId and upper(coalesce(codigo,''))=@Codigo and ativo and is_deleted=false and status not in ('ENCERRADA','ENCERRADO','CANCELADA','CANCELADO'))",new{TenantId=x.TenantId,Codigo=codigo},tx,cancellationToken:ct)).ConfigureAwait(false);
    if(existe)throw new IndicadorCodigoExistenteException(codigo);
    long id;
    try{
      id=await c.ExecuteScalarAsync<long>(new CommandDefinition("insert into sigov.educacao_indicador(tenant_id,entidade_id,codigo,nome,tipo,descricao,status,auditoria,correlation_id,created_by) values(@TenantId,@EntidadeId,@Codigo,@Nome,coalesce(@Tipo,'INDICADOR'),@Descricao,'ATIVA',jsonb_build_array(jsonb_build_object('acao','LANCAR_INDICADOR','em',now())),@CorrelationId,@UsuarioId) returning id",new{TenantId=x.TenantId,EntidadeId=x.EntidadeId,Codigo=codigo,Nome=r.Nome.Trim(),Tipo=string.IsNullOrWhiteSpace(r.Tipo)?null:r.Tipo.Trim().ToUpperInvariant(),Descricao=r.Descricao?.Trim(),CorrelationId=corr,UsuarioId=x.UsuarioId},tx,cancellationToken:ct)).ConfigureAwait(false);
    }catch(NpgsqlException npe){
      if(npe.SqlState=="23505")throw new IndicadorCodigoExistenteException(codigo);
      throw;
    }
    await tx.CommitAsync(ct).ConfigureAwait(false);
    return id;
  }
  public async Task<long> LancarIndicadorValorAsync(EducacaoAvancadaContext x,EducacaoIndicadorValorLancarRequest r,CancellationToken ct){
    if(!await _inspector.TableExistsAsync("sigov","educacao_indicador",ct).ConfigureAwait(false))throw new RecursoNaoDisponivelException("educacao_indicador");
    if(!await _inspector.TableExistsAsync("sigov","educacao_indicador_valor",ct).ConfigureAwait(false))throw new RecursoNaoDisponivelException("educacao_indicador_valor");
    if(r is null)throw new ArgumentException("Valor de indicador é obrigatório.");
    if(r.IndicadorId<=0)throw new ArgumentException("Valor de indicador exige indicador.");
    if(r.Ano is <2000 or >2100)throw new ArgumentException("Ano do valor de indicador deve estar entre 2000 e 2100.");
    if(r.Valor<=0)throw new ArgumentException("Valor do indicador deve ser positivo.");
    var corr=Guid.TryParse(x.CorrelationId,out var g)?g:Guid.NewGuid();
    using var c=(NpgsqlConnection)_context.CreateConnection();
    await c.OpenAsync(ct).ConfigureAwait(false);
    await using var tx=await c.BeginTransactionAsync(ct).ConfigureAwait(false);
    var ind=await c.QueryFirstOrDefaultAsync<EducacaoIndicadorLinha>(new CommandDefinition("select id as Id,entidade_id as EntidadeId,status as Status from sigov.educacao_indicador where tenant_id=@TenantId and id=@Id and ativo and not is_deleted",new{TenantId=x.TenantId,Id=r.IndicadorId},tx,cancellationToken:ct)).ConfigureAwait(false);
    if(ind is null)throw new IndicadorInexistenteException(r.IndicadorId);
    if(ind.EntidadeId>=0&&x.EntidadeId.HasValue&&ind.EntidadeId.Value!=x.EntidadeId.Value)throw new ContextoInstitucionalDivergenteException("educacao_indicador",r.IndicadorId,ind.EntidadeId.Value,x.EntidadeId.Value);
    if(ind.Status is not ("ATIVA" or "ATIVO"))throw new IndicadorNaoVigenteException(r.IndicadorId,ind.Status ?? "INDEFINIDO");
    var existe=await c.ExecuteScalarAsync<bool>(new CommandDefinition("select exists(select 1 from sigov.educacao_indicador_valor where tenant_id=@TenantId and referencia_id=@IndicadorId and ano_letivo=@Ano and ativo and is_deleted=false and status not in ('ENCERRADA','ENCERRADO','CANCELADA','CANCELADO'))",new{TenantId=x.TenantId,IndicadorId=r.IndicadorId,Ano=r.Ano},tx,cancellationToken:ct)).ConfigureAwait(false);
    if(existe)throw new ValorIndicadorExistenteException(r.IndicadorId,r.Ano);
    long id;
    try{
      id=await c.ExecuteScalarAsync<long>(new CommandDefinition("insert into sigov.educacao_indicador_valor(tenant_id,entidade_id,referencia_id,ano_letivo,tipo,valor,status,auditoria,correlation_id,created_by) values(@TenantId,@EntidadeId,@IndicadorId,@Ano,'INDICADOR_VALOR',@Valor,'ATIVA',jsonb_build_array(jsonb_build_object('acao','LANCAR_INDICADOR_VALOR','em',now())),@CorrelationId,@UsuarioId) returning id",new{TenantId=x.TenantId,EntidadeId=x.EntidadeId,IndicadorId=r.IndicadorId,Ano=r.Ano,Valor=r.Valor,CorrelationId=corr,UsuarioId=x.UsuarioId},tx,cancellationToken:ct)).ConfigureAwait(false);
    }catch(NpgsqlException npe){
      if(npe.SqlState=="23505")throw new ValorIndicadorExistenteException(r.IndicadorId,r.Ano);
      throw;
    }
    await tx.CommitAsync(ct).ConfigureAwait(false);
    return id;
  }
  public async Task<long> LancarProjecaoMatriculaAsync(EducacaoAvancadaContext x,EducacaoProjecaoMatriculaLancarRequest r,CancellationToken ct){
    if(!await _inspector.TableExistsAsync("sigov","educacao_projecao_matricula",ct).ConfigureAwait(false))throw new RecursoNaoDisponivelException("educacao_projecao_matricula");
    if(r is null)throw new ArgumentException("Projeção de matrícula é obrigatória.");
    if(r.EscolaId<=0)throw new ArgumentException("Projeção de matrícula exige escola.");
    if(r.Ano is <2000 or >2100)throw new ArgumentException("Ano da projeção de matrícula deve estar entre 2000 e 2100.");
    if(r.Quantidade<=0)throw new ArgumentException("Quantidade da projeção de matrícula deve ser positiva.");
    await ValidarEscopoInstitucionalAsync(x.TenantId,x.EntidadeId,r.EscolaId,null,ct).ConfigureAwait(false);
    var corr=Guid.TryParse(x.CorrelationId,out var g)?g:Guid.NewGuid();
    using var c=(NpgsqlConnection)_context.CreateConnection();
    await c.OpenAsync(ct).ConfigureAwait(false);
    await using var tx=await c.BeginTransactionAsync(ct).ConfigureAwait(false);
    var existe=await c.ExecuteScalarAsync<bool>(new CommandDefinition("select exists(select 1 from sigov.educacao_projecao_matricula where tenant_id=@TenantId and escola_id=@EscolaId and ano_letivo=@Ano and ativo and is_deleted=false and status not in ('ENCERRADA','ENCERRADO','CANCELADA','CANCELADO'))",new{TenantId=x.TenantId,EscolaId=r.EscolaId,Ano=r.Ano},tx,cancellationToken:ct)).ConfigureAwait(false);
    if(existe)throw new ProjecaoMatriculaExistenteException(r.EscolaId,r.Ano);
    long id;
    try{
      id=await c.ExecuteScalarAsync<long>(new CommandDefinition("insert into sigov.educacao_projecao_matricula(tenant_id,entidade_id,escola_id,ano_letivo,quantidade,tipo,status,auditoria,correlation_id,created_by) values(@TenantId,@EntidadeId,@EscolaId,@Ano,@Quantidade,'PROJECAO_MATRICULA','ATIVA',jsonb_build_array(jsonb_build_object('acao','LANCAR_PROJECAO_MATRICULA','em',now())),@CorrelationId,@UsuarioId) returning id",new{TenantId=x.TenantId,EntidadeId=x.EntidadeId,EscolaId=r.EscolaId,Ano=r.Ano,Quantidade=r.Quantidade,CorrelationId=corr,UsuarioId=x.UsuarioId},tx,cancellationToken:ct)).ConfigureAwait(false);
    }catch(NpgsqlException npe){
      if(npe.SqlState=="23505")throw new ProjecaoMatriculaExistenteException(r.EscolaId,r.Ano);
      throw;
    }
    await tx.CommitAsync(ct).ConfigureAwait(false);
    return id;
  }
  public async Task<long> LancarEducacensoLoteAsync(EducacaoAvancadaContext x,EducacaoEducacensoLoteLancarRequest r,CancellationToken ct){
    if(!await _inspector.TableExistsAsync("sigov","educacao_educacenso_lote",ct).ConfigureAwait(false))throw new RecursoNaoDisponivelException("educacao_educacenso_lote");
    if(r is null)throw new ArgumentException("Lote Educacenso é obrigatório.");
    if(r.Ano is <2000 or >2100)throw new ArgumentException("Ano do lote Educacenso deve estar entre 2000 e 2100.");
    var corr=Guid.TryParse(x.CorrelationId,out var g)?g:Guid.NewGuid();
    using var c=(NpgsqlConnection)_context.CreateConnection();
    await c.OpenAsync(ct).ConfigureAwait(false);
    await using var tx=await c.BeginTransactionAsync(ct).ConfigureAwait(false);
    var existe=await c.ExecuteScalarAsync<bool>(new CommandDefinition("select exists(select 1 from sigov.educacao_educacenso_lote where tenant_id=@TenantId and ano_letivo=@Ano and ativo and is_deleted=false and status not in ('ENCERRADA','ENCERRADO','CANCELADA','CANCELADO'))",new{TenantId=x.TenantId,Ano=r.Ano},tx,cancellationToken:ct)).ConfigureAwait(false);
    if(existe)throw new LoteEducacensoAnoExistenteException(r.Ano);
    long id;
    try{
      id=await c.ExecuteScalarAsync<long>(new CommandDefinition("insert into sigov.educacao_educacenso_lote(tenant_id,entidade_id,ano_letivo,tipo,status,auditoria,correlation_id,created_by) values(@TenantId,@EntidadeId,@Ano,'EDUCACENSO','ATIVA',jsonb_build_array(jsonb_build_object('acao','LANCAR_LOTE_EDUCACENSO','em',now())),@CorrelationId,@UsuarioId) returning id",new{TenantId=x.TenantId,EntidadeId=x.EntidadeId,Ano=r.Ano,CorrelationId=corr,UsuarioId=x.UsuarioId},tx,cancellationToken:ct)).ConfigureAwait(false);
    }catch(NpgsqlException npe){
      if(npe.SqlState=="23505")throw new LoteEducacensoAnoExistenteException(r.Ano);
      throw;
    }
    await tx.CommitAsync(ct).ConfigureAwait(false);
    return id;
  }
  public async Task<long> LancarEducacensoInconsistenciaAsync(EducacaoAvancadaContext x,EducacaoEducacensoInconsistenciaLancarRequest r,CancellationToken ct){
    if(!await _inspector.TableExistsAsync("sigov","educacao_educacenso_inconsistencia",ct).ConfigureAwait(false))throw new RecursoNaoDisponivelException("educacao_educacenso_inconsistencia");
    if(!await _inspector.TableExistsAsync("sigov","educacao_educacenso_lote",ct).ConfigureAwait(false))throw new RecursoNaoDisponivelException("educacao_educacenso_lote");
    if(r is null)throw new ArgumentException("Inconsistência Educacenso é obrigatória.");
    if(r.LoteId<=0)throw new ArgumentException("Inconsistência Educacenso exige lote.");
    if(string.IsNullOrWhiteSpace(r.Gravidade))throw new ArgumentException("Inconsistência Educacenso exige gravidade.");
    var gravidade=r.Gravidade.Trim().ToUpperInvariant();
    if(gravidade is not ("BAIXA" or "MEDIA" or "ALTA"))throw new ArgumentException("Gravidade da inconsistência Educacenso deve ser BAIXA, MEDIA ou ALTA.");
    if(string.IsNullOrWhiteSpace(r.Origem))throw new ArgumentException("Inconsistência Educacenso exige origem (registro/fluxo de onde ela partiu).");
    var corr=Guid.TryParse(x.CorrelationId,out var g)?g:Guid.NewGuid();
    using var c=(NpgsqlConnection)_context.CreateConnection();
    await c.OpenAsync(ct).ConfigureAwait(false);
    var lote=await c.QueryFirstOrDefaultAsync<EducacaoEducacensoLoteLinha>(new CommandDefinition("select id as Id,entidade_id as EntidadeId,status as Status from sigov.educacao_educacenso_lote where tenant_id=@TenantId and id=@Id and ativo and not is_deleted",new{TenantId=x.TenantId,Id=r.LoteId},cancellationToken:ct)).ConfigureAwait(false);
    if(lote is null)throw new LoteEducacensoInexistenteException(r.LoteId);
    if(lote.EntidadeId>=0&&x.EntidadeId.HasValue&&lote.EntidadeId.Value!=x.EntidadeId.Value)throw new ContextoInstitucionalDivergenteException("educacao_educacenso_lote",r.LoteId,lote.EntidadeId.Value,x.EntidadeId.Value);
    if(lote.Status is not ("ATIVA" or "ATIVO"))throw new LoteEducacensoNaoVigenteException(r.LoteId,lote.Status ?? "INDEFINIDO");
    return await c.ExecuteScalarAsync<long>(new CommandDefinition("insert into sigov.educacao_educacenso_inconsistencia(tenant_id,entidade_id,referencia_id,tipo,nome,descricao,status,auditoria,correlation_id,created_by) values(@TenantId,@EntidadeId,@LoteId,@Gravidade,@Origem,@Recomendacao,'ABERTA',jsonb_build_array(jsonb_build_object('acao','LANCAR_INCONSISTENCIA_EDUCACENSO','em',now())),@CorrelationId,@UsuarioId) returning id",new{TenantId=x.TenantId,EntidadeId=x.EntidadeId,LoteId=r.LoteId,Gravidade=gravidade,Origem=r.Origem.Trim().ToUpperInvariant(),Recomendacao=r.Recomendacao?.Trim(),CorrelationId=corr,UsuarioId=x.UsuarioId},cancellationToken:ct)).ConfigureAwait(false);
  }
  public async Task<EducacaoEducacensoDashboardDto> EducacensoDashboardAsync(long tenantId,CancellationToken ct){
    foreach(var t in new[]{"educacao_educacenso_lote","educacao_educacenso_inconsistencia","educacao_educacenso_evento"})if(!await _inspector.TableExistsAsync("sigov",t,ct).ConfigureAwait(false))throw new RecursoNaoDisponivelException(t);
    using var c=_context.CreateConnection();
    var sql="select (select count(1) from sigov.educacao_educacenso_lote where tenant_id=@TenantId and ativo and not is_deleted and status not in ('ENCERRADA','ENCERRADO','CANCELADA','CANCELADO')) as LotesAtivos,(select count(1) from sigov.educacao_educacenso_inconsistencia where tenant_id=@TenantId and ativo and not is_deleted and status not in ('ENCERRADA','ENCERRADO','CANCELADA','CANCELADO')) as InconsistenciasAbertas,(select count(1) from sigov.educacao_educacenso_inconsistencia where tenant_id=@TenantId and ativo and not is_deleted and upper(coalesce(tipo,''))='ALTA' and status not in ('ENCERRADA','ENCERRADO','CANCELADA','CANCELADO')) as GravidadeAlta,(select count(1) from sigov.educacao_educacenso_evento where tenant_id=@TenantId and not is_deleted) as Eventos";
    var m=await c.QueryFirstAsync<EducacaoEducacensoMetricasLinha>(new CommandDefinition(sql,new{TenantId=tenantId},cancellationToken:ct)).ConfigureAwait(false);
    return new EducacaoEducacensoDashboardDto(m.LotesAtivos,m.InconsistenciasAbertas,m.GravidadeAlta,m.Eventos);
  }
 public async Task<bool> AlterarStatusAsync(EducacaoAvancadaContext x,string recurso,long id,string status,string? justificativa,CancellationToken ct,DateTime? versaoAtual=null){recurso=Recurso(recurso);if(string.IsNullOrWhiteSpace(status))throw new ArgumentException("Status é obrigatório.");var alvo=status.Trim().ToUpperInvariant();if ((alvo is "CANCELADA" or "CANCELADO") && string.IsNullOrWhiteSpace(justificativa))throw new ArgumentException("Cancelamento exige justificativa.");if(!await _inspector.TableExistsAsync("sigov",recurso,ct).ConfigureAwait(false))throw new RecursoNaoDisponivelException(recurso);using var c=_context.CreateConnection();var de=await c.QueryFirstOrDefaultAsync<(string? Status,long LinhaEntidade,DateTime? LinhaUpdatedAt)>(new CommandDefinition($"select status,coalesce(entidade_id,-1) as LinhaEntidade,updated_at as LinhaUpdatedAt from sigov.{recurso} where tenant_id=@TenantId and id=@Id and is_deleted=false",new{x.TenantId,Id=id},cancellationToken:ct));if(de.Status is null)throw new RegistroNaoEncontradoException(recurso,id);if(de.LinhaEntidade>=0 && x.EntidadeId.HasValue && x.EntidadeId.Value!=de.LinhaEntidade)throw new ContextoInstitucionalDivergenteException(recurso,id,de.LinhaEntidade,x.EntidadeId.Value);var linhaDe=de.Status;if(!EducacaoStatusMaquina.TransicaoPermitida(linhaDe,alvo))throw new TransicaoDeStatusInvalidaException(linhaDe,alvo);
    // C2S2 BR-2.1: origem fora das familias canonicas = LEGADO_NAO_REGULARIZADO — regularizacao visivel e auditavel
    if(EducacaoStatusMaquina.Grupo(linhaDe) is null && string.IsNullOrWhiteSpace(justificativa))throw new RegularizacaoLegadaExigeJustificativaException(linhaDe);if(EducacaoEfeitosTransicao.ExigeCompensacaoAntesDeGravar(recurso,alvo)){var comEfeito=await c.ExecuteScalarAsync<bool>(new CommandDefinition("select exists(select 1 from sigov.educacao_merenda_movimento where tenant_id=@TenantId and referencia_id=@Id and is_deleted=false)",new{TenantId=x.TenantId,Id=id},cancellationToken:ct)).ConfigureAwait(false);if(comEfeito)throw new DistribuicaoEstornoObrigatorioException(id);}
    // C2S2 BR-2.3: registro ja atualizado exige updatedAt (CONFLITO_VERSAO quando omitido ou obsoleto)
    if(de.LinhaUpdatedAt is not null && versaoAtual is null)throw new ConflitoDeVersaoEducacaoException(recurso,id);
    var n=await c.ExecuteAsync(new CommandDefinition($"update sigov.{recurso} set status=@Status,justificativa=coalesce(@Justificativa,justificativa),updated_at=now(),updated_by=@UsuarioId,correlation_id=@CorrelationId,auditoria=(case jsonb_typeof(auditoria) when 'array' then auditoria when 'object' then case when auditoria='{{}}'::jsonb then '[]'::jsonb else jsonb_build_array(auditoria) end else '[]'::jsonb end)||jsonb_build_array(jsonb_build_object('acao','STATUS','de',@De,'para',@Status,'motivo',coalesce(@Justificativa,justificativa),'em',now())) where tenant_id=@TenantId and id=@Id and is_deleted=false and (@VersaoAtual::timestamptz is null or coalesce(updated_at,created_at)=@VersaoAtual)",new{x.TenantId,Id=id,Status=alvo,De=linhaDe,Justificativa=justificativa,x.UsuarioId,CorrelationId=Guid.TryParse(x.CorrelationId,out var g)?g:Guid.NewGuid(),VersaoAtual=versaoAtual},cancellationToken:ct));if(n==0)throw new ConflitoDeVersaoEducacaoException(recurso,id);return true;}
 public async Task<EducacaoAvancadaDashboardDto> DashboardAsync(long tenantId,string recurso,CancellationToken ct){recurso=Recurso(recurso);if(!await _inspector.TableExistsAsync("sigov",recurso,ct).ConfigureAwait(false))throw new RecursoNaoDisponivelException(recurso);using var c=_context.CreateConnection();var sql=$"select count(1) as total,count(1) filter (where status in ('ATIVA','ATIVO','PUBLICADO','DISPONIVEL','ABERTO')) as ativos,count(1) filter (where status in ('RASCUNHO','PROGRAMADA','SOLICITADA')) as pendentes,count(1) filter (where status in ('ATRASADO','SUSPENSA','COM_INCONSISTENCIA')) as alertas from sigov.{recurso} where tenant_id=@TenantId and is_deleted=false;select id as Id,codigo as Codigo,nome as Nome,status as Status,tipo as Tipo,descricao as Descricao,quantidade as Quantidade,valor as Valor,created_at as \"CreatedAt\",coalesce(updated_at,created_at) as \"UpdatedAt\" from sigov.{recurso} where tenant_id=@TenantId and is_deleted=false order by id desc limit 6;";using var m=await c.QueryMultipleAsync(new CommandDefinition(sql,new{TenantId=tenantId},cancellationToken:ct));var tot=await m.ReadFirstAsync<EducacaoTotais>();var recentes=(await m.ReadAsync<EducacaoAvancadaRegistroDto>()).ToList();return new(tot.Total,tot.Ativos,tot.Pendentes,tot.Alertas,recentes);}
 public async Task<byte[]> ExportarCsvAsync(long tenantId,string recurso,CancellationToken ct){recurso=Recurso(recurso);if(!await _inspector.TableExistsAsync("sigov",recurso,ct).ConfigureAwait(false))throw new RecursoNaoDisponivelException(recurso);using var c=_context.CreateConnection();var sql=$"select id as Id,codigo as Codigo,nome as Nome,status as Status,tipo as Tipo,descricao as Descricao,quantidade as Quantidade,valor as Valor,created_at as \"CreatedAt\",coalesce(updated_at,created_at) as \"UpdatedAt\" from sigov.{recurso} where tenant_id=@TenantId and is_deleted=false order by id";var r=(await c.QueryAsync<EducacaoAvancadaRegistroDto>(new CommandDefinition(sql,new{TenantId=tenantId},cancellationToken:ct))).ToList();var b=new StringBuilder("id;codigo;nome;tipo;status;valor\n");foreach(var x in r){b.Append(x.Id).Append(';').Append(EducacaoCsv.Campo(x.Codigo)).Append(';').Append(EducacaoCsv.Campo(x.Nome)).Append(';').Append(EducacaoCsv.Campo(x.Tipo)).Append(';').Append(EducacaoCsv.Campo(x.Status)).Append(';').Append(x.Valor?.ToString(System.Globalization.CultureInfo.InvariantCulture)).Append('\n');}return System.Text.Encoding.UTF8.GetPreamble().Concat(System.Text.Encoding.UTF8.GetBytes(b.ToString())).ToArray();}
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
