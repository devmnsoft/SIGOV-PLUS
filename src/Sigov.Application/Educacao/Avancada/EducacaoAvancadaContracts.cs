using Sigov.Application.Common;
namespace Sigov.Application.Educacao.Avancada;
public sealed record EducacaoAvancadaContext(long TenantId,long? EntidadeId,long? ExercicioId,long? UsuarioId,string CorrelationId);
public sealed record EducacaoAvancadaFiltro(long? EscolaId=null,int? AnoLetivo=null,string? Status=null,int Pagina=1,int Tamanho=30);
public sealed record EducacaoAvancadaRegistroDto(long Id,string? Codigo,string? Nome,string Status,string? Tipo,string? Descricao,decimal? Quantidade,decimal? Valor,DateTime CreatedAt,DateTime? UpdatedAt=null);
public sealed record EducacaoAvancadaOperacaoRequest(long? EscolaId,long? AlunoId,long? ReferenciaId,string? Codigo,string? Nome,string? Tipo,string Status,string? Descricao,string? Justificativa,string? Turno,decimal? Quantidade,decimal? Valor,int? AnoLetivo,IReadOnlyDictionary<string,object?>? Dados);
public sealed record EducacaoAvancadaDashboardDto(long Total,long Ativos,long Pendentes,long Alertas,IReadOnlyCollection<EducacaoAvancadaRegistroDto> Recentes);
public sealed record EducacaoTransporteDashboardDto(long RotasAtivas,long PontosAtivos,long AlunosVinculados,long EscolasAtendidas);
public sealed record EducacaoTransporteRotaDto(long Id,string Nome,string Turno,string Status,long? EscolaId);
public sealed record EducacaoTransporteCriarRotaRequest(string Nome,string Turno,string Status,long? EscolaId,string? Regiao);
public sealed record EducacaoTransporteRotaPontoDto(long Id,long RotaId,int Ordem,string Descricao);
public sealed record EducacaoTransporteVeiculoDto(long Id,string Codigo,string Status);
public sealed record EducacaoTransporteMotoristaDto(long Id,string Nome,string Status);
public sealed record EducacaoTransporteAlunoDto(long Id,long AlunoId,long RotaId,string Turno,string Status);
public sealed record EducacaoTransporteVincularAlunoRequest(long AlunoId,long? PontoId,string? Turno);
public sealed record EducacaoTransporteViagemDto(long Id,long RotaId,string Turno,string Status);
public sealed record EducacaoTransporteCriarViagemRequest(long RotaId,long VeiculoId,long MotoristaId,DateOnly Data,string Turno);
public sealed record EducacaoTransporteFrequenciaDto(long Id,long AlunoId,bool Presente);
public sealed record EducacaoTransporteOcorrenciaDto(long Id,string Tipo,string Descricao);
public sealed record EducacaoTransporteCustoDto(long Id,decimal Valor,string Tipo);
// Ciclo 2 Sprint 4 (transporte escolar: da rota à viagem, BR-4.1 a BR-4.8): comandos tipados de
// veiculo/motorista/viagem/presenca/ocorrencia/custo. Motivos/justificativas nullables para a
// validacao nomeada do repositório ser a autoridade (regra 13), nao o [ApiController].
public sealed record EducacaoTransporteLancarVeiculoRequest(string Placa,int Capacidade,string Classe,string? SituacaoDocumental,DateOnly? InspecaoValidade);
public sealed record EducacaoTransporteLancarMotoristaRequest(string Nome,string HabilitacaoCategoria,DateOnly HabilitacaoValidade);
public sealed record EducacaoTransporteLancarViagemRequest(long RotaId,DateOnly Data,long VeiculoId,long MotoristaId,IReadOnlyCollection<long>? MonitorIds=null,decimal KmEstimado=0m,decimal TarifaKm=0m,decimal CombustivelEstimado=0m,decimal PedagioPrevisto=0m);
public sealed record EducacaoTransporteIniciarViagemRequest(DateTime? HoraInicio);
public sealed record EducacaoTransporteConcluirViagemRequest(DateTime? HoraFim,decimal? KmPercorridos);
public sealed record EducacaoTransporteCancelarViagemRequest(string? Justificativa,long? OcorrenciaId=null);
public sealed record EducacaoTransporteSubstituirViagemRequest(long? VeiculoId,long? MotoristaId,string Justificativa);
public sealed record EducacaoTransporteLancarPresencaRequest(long AlunoId,string? Status);
public sealed record EducacaoTransporteEstornarPresencaRequest(string? Justificativa);
public sealed record EducacaoTransporteLancarOcorrenciaViagemRequest(string? Tipo,string? Gravidade,string? Descricao,DateTime? DataHora=null);
public sealed record EducacaoTransporteDesfecharOcorrenciaRequest(string? Relatorio);
public sealed record EducacaoTransporteRegistrarCustoRealizadoRequest(decimal Valor,string? Documento=null);
public sealed record EducacaoTransporteRelatorioViagemAlunoDto(long AlunoId,string Nome,string Status);
public sealed record EducacaoTransporteRelatorioViagemOcorrenciaDto(long Id,string? Tipo,string? Gravidade,string? Descricao,DateTime? DataHora,DateTime? DesfechoEm);
public sealed record EducacaoTransporteRelatorioViagemDto(long ViagemId,DateOnly Data,long RotaId,long VeiculoId,long MotoristaId,int Versao,string Status,int AlunosVinculados,int Presentes,int Faltantes,int Desligados,decimal KmEstimado,decimal? KmPercorridos,decimal CustoEstimado,decimal? CustoRealizado,decimal? DesvioCustoAbsoluto,decimal? DesvioCustoPct,IReadOnlyCollection<EducacaoTransporteRelatorioViagemAlunoDto> Alunos,IReadOnlyCollection<EducacaoTransporteRelatorioViagemOcorrenciaDto> Ocorrencias,string Origem,DateTime GeradoEm);
public sealed record EducacaoMerendaDashboardDto(long Produtos,long EstoqueCritico,long CardapiosPublicados,long Requisicoes);
public sealed record EducacaoMerendaProdutoDto(long Id,string Nome,string Unidade,string Categoria,string Status);
public sealed record EducacaoMerendaCriarProdutoRequest(string Nome,string Unidade,string Categoria,decimal SaldoMinimo);
public sealed record EducacaoMerendaFornecedorDto(long Id,string Nome,string DocumentoMascarado);
public sealed record EducacaoMerendaEstoqueDto(long ProdutoId,decimal Saldo,decimal SaldoMinimo);
public sealed record EducacaoMerendaMovimentoDto(long Id,string Tipo,decimal Quantidade);
public sealed record EducacaoMerendaCriarMovimentoRequest(long ProdutoId,string Tipo,decimal Quantidade,string? Justificativa);
public sealed record EducacaoMerendaCardapioDto(long Id,long EscolaId,string Status,DateOnly Inicio,DateOnly Fim);
public sealed record EducacaoMerendaCriarCardapioRequest(long EscolaId,DateOnly Inicio,DateOnly Fim,IReadOnlyCollection<long> Produtos);
public sealed record EducacaoMerendaRegistrarEstoqueRequest(long ProdutoId,decimal SaldoInicial);
public sealed record EducacaoMerendaDistribuirItemRequest(long ProdutoId,decimal Quantidade);
public sealed record EducacaoMerendaDistribuirRefeicaoRequest(long CardapioId,DateOnly Data,IReadOnlyCollection<EducacaoMerendaDistribuirItemRequest> Itens,string? ChaveIdempotencia=null);
// Ciclo 2 Sprint 1 (merenda integridade): comandos tipados de estoque/estorno. Operacao =
// ABERTURA|ENTRADA|AJUSTE; AJUSTE exige Motivo; ChaveIdempotencia torna o reenvio efeito único.
public sealed record EducacaoMerendaEstoqueLancarRequest(long ProdutoId,string Operacao,decimal Quantidade,string? Lote=null,DateOnly? Validade=null,string? Origem=null,string? Motivo=null,string? ChaveIdempotencia=null);
public sealed record EducacaoMerendaEstornoRequest(string Motivo);
public sealed record EducacaoMerendaDistribuicaoDto(long Id,long EscolaId,string Status);
public sealed record EducacaoMerendaRequisicaoDto(long Id,long EscolaId,string Status);
public sealed record EducacaoMerendaConsumoDto(long Id,long EscolaId,decimal Quantidade);
public sealed record EducacaoBibliotecaDashboardDto(long Titulos,long Disponiveis,long EmprestimosAtivos,long Atrasos,long ReservasPendentes,long Bloqueados);
public sealed record EducacaoBibliotecaAcervoDto(long Id,string Titulo,string Tipo,string Status);
public sealed record EducacaoBibliotecaCriarAcervoRequest(string Titulo,string Tipo);
public sealed record EducacaoBibliotecaExemplarDto(long Id,long AcervoId,string Codigo,string Status);
public sealed record EducacaoBibliotecaEmprestimoDto(long Id,long ExemplarId,long? AlunoId,long? ServidorId,string Status);
public sealed record EducacaoBibliotecaCriarEmprestimoRequest(long ExemplarId,long? AlunoId,long? ServidorId,DateOnly Prazo);
public sealed record EducacaoBibliotecaDevolverRequest(string? Observacao,DateOnly? DataDevolucao=null);
public sealed record EducacaoBibliotecaCriarReservaRequest(long ExemplarId,long? AlunoId,long? ServidorId);
public sealed record EducacaoBibliotecaReservaDto(long Id,long ExemplarId,string Status);
public sealed record EducacaoBibliotecaOcorrenciaLancarRequest(long ExemplarId,string? Tipo,string? Motivo,decimal? ValorEstimado=null,DateOnly? Data=null);
public sealed record EducacaoBibliotecaEstornoOcorrenciaRequest(string? Motivo);
public sealed record EducacaoBibliotecaDesbloquearRequest(string? Justificativa);
public sealed record EducacaoBibliotecaInventarioLancarRequest(long? EscolaId=null);
public sealed record EducacaoBibliotecaInventarioContarRequest(long ExemplarId,int Quantidade,string? StatusContado=null);
public sealed record EducacaoBibliotecaInventarioJustificarRequest(long ExemplarId,string? Desfecho,string? Justificativa);
public sealed record EducacaoBibliotecaHistoricoItemDto(DateTime Em,string Tipo,string Descricao,long? ReferenciaId);
public sealed record EducacaoTransporteLancarRotaRequest(string Nome,string Turno,long? EscolaId,string? Regiao,string? Codigo);
public sealed record EducacaoTransporteLancarPontoRequest(string Nome,int Ordem,string? Descricao);
public sealed record EducacaoBibliotecaOcorrenciaDto(long Id,string Motivo);
public sealed record EducacaoBibliotecaDigitalReferenciaDto(long Id,long AcervoId,long? GedDocumentoId);
public sealed record EducacaoFundebLancarParametroRequest(string Codigo,decimal Valor);
public sealed record EducacaoFundebLancarReceitaRequest(DateOnly Competencia,string Fonte,decimal Valor);
public sealed record EducacaoFundebLancarAplicacaoRequest(long CentroCustoId,decimal Valor);
public sealed record EducacaoCustoLancarCentroRequest(string Codigo,string Nome);
public sealed record EducacaoIndicadoresDashboardDto(decimal ReceitasFundeb,decimal AplicacoesFundeb,decimal TaxaAplicacao,decimal? TaxaMinima,bool EmConformidade,decimal CustoPorAluno,long Inconsistencias);
public sealed record EducacaoFundebParametroDto(long Id,string Codigo,decimal Valor);
public sealed record EducacaoFundebReceitaDto(long Id,DateOnly Competencia,string Fonte,decimal Valor);
public sealed record EducacaoFundebAplicacaoDto(long Id,long CentroCustoId,decimal Valor);
public sealed record EducacaoCustoCentroDto(long Id,string Nome,string Status);
public sealed record EducacaoCustoLancamentoDto(long Id,long CentroCustoId,decimal Valor);
public sealed record EducacaoCustoRateioDto(long Id,string Criterio,decimal Valor);
public sealed record EducacaoIndicadorDto(long Id,string Codigo,string Nome);
public sealed record EducacaoIndicadorValorDto(long Id,long IndicadorId,int Ano,decimal Valor);
public sealed record EducacaoProjecaoMatriculaDto(long Id,long EscolaId,int Ano,int Quantidade);
public sealed record EducacaoEducacensoLoteDto(long Id,int Ano,string Status);
public sealed record EducacaoEducacensoInconsistenciaDto(long Id,string Gravidade,string Origem,string Recomendacao);
public sealed record EducacaoAvaliacaoMagisterioDto(long Id,string Nome,string Status);
public sealed record EducacaoAvaliacaoResultadoDto(long Id,long AvaliacaoId,decimal Resultado);
public sealed record EducacaoIndicadorLancarRequest(string Codigo,string Nome,string? Tipo,string? Descricao);
public sealed record EducacaoIndicadorValorLancarRequest(long IndicadorId,int Ano,decimal Valor);
public sealed record EducacaoProjecaoMatriculaLancarRequest(long EscolaId,int Ano,int Quantidade);
public sealed record EducacaoEducacensoLoteLancarRequest(int Ano);
public sealed record EducacaoEducacensoInconsistenciaLancarRequest(long LoteId,string Gravidade,string Origem,string? Recomendacao);
public sealed record EducacaoEducacensoDashboardDto(long LotesAtivos,long InconsistenciasAbertas,long GravidadeAlta,long Eventos);
public interface IEducacaoAvancadaRepository { Task<PagedResult<EducacaoAvancadaRegistroDto>> ListarAsync(long tenantId,string recurso,EducacaoAvancadaFiltro filtro,CancellationToken ct); Task<long> CriarAsync(EducacaoAvancadaContext contexto,string recurso,EducacaoAvancadaOperacaoRequest request,CancellationToken ct); Task<bool> AlterarStatusAsync(EducacaoAvancadaContext contexto,string recurso,long id,string status,string? justificativa,CancellationToken ct,DateTime? versaoAtual=null); Task<EducacaoAvancadaDashboardDto> DashboardAsync(long tenantId,string recurso,CancellationToken ct); Task<byte[]> ExportarCsvAsync(long tenantId,string recurso,CancellationToken ct); }
public interface IEducacaoTransporteEscolarRepository:IEducacaoAvancadaRepository{} public interface IEducacaoTransporteEscolarService:IEducacaoAvancadaRepository{Task<long> LancarRotaAsync(EducacaoAvancadaContext contexto,EducacaoTransporteLancarRotaRequest request,CancellationToken ct);Task<long> LancarPontoAsync(EducacaoAvancadaContext contexto,long rotaId,EducacaoTransporteLancarPontoRequest request,CancellationToken ct);Task<long> VincularAlunoAsync(EducacaoAvancadaContext contexto,long rotaId,EducacaoTransporteVincularAlunoRequest request,CancellationToken ct);Task<EducacaoTransporteDashboardDto> TransporteDashboardAsync(long tenantId,CancellationToken ct);Task<long> LancarVeiculoAsync(EducacaoAvancadaContext contexto,EducacaoTransporteLancarVeiculoRequest request,CancellationToken ct);Task<long> LancarMotoristaAsync(EducacaoAvancadaContext contexto,EducacaoTransporteLancarMotoristaRequest request,CancellationToken ct);Task<long> LancarViagemAsync(EducacaoAvancadaContext contexto,EducacaoTransporteLancarViagemRequest request,CancellationToken ct);Task<long> IniciarViagemAsync(EducacaoAvancadaContext contexto,long viagemId,EducacaoTransporteIniciarViagemRequest request,CancellationToken ct);Task<long> ConcluirViagemAsync(EducacaoAvancadaContext contexto,long viagemId,EducacaoTransporteConcluirViagemRequest request,CancellationToken ct);Task<long> CancelarViagemAsync(EducacaoAvancadaContext contexto,long viagemId,EducacaoTransporteCancelarViagemRequest request,CancellationToken ct);Task<long> SubstituirViagemAsync(EducacaoAvancadaContext contexto,long viagemId,EducacaoTransporteSubstituirViagemRequest request,CancellationToken ct);Task<long> LancarPresencaAsync(EducacaoAvancadaContext contexto,long viagemId,EducacaoTransporteLancarPresencaRequest request,CancellationToken ct);Task<long> FecharPresencasAsync(EducacaoAvancadaContext contexto,long viagemId,CancellationToken ct);Task<long> EstornarPresencaAsync(EducacaoAvancadaContext contexto,long viagemId,long presencaId,EducacaoTransporteEstornarPresencaRequest request,CancellationToken ct);Task<long> LancarOcorrenciaViagemAsync(EducacaoAvancadaContext contexto,long viagemId,EducacaoTransporteLancarOcorrenciaViagemRequest request,CancellationToken ct);Task<long> DesfecharOcorrenciaViagemAsync(EducacaoAvancadaContext contexto,long viagemId,long ocorrenciaId,EducacaoTransporteDesfecharOcorrenciaRequest request,CancellationToken ct);Task<long> RegistrarCustoRealizadoAsync(EducacaoAvancadaContext contexto,long viagemId,EducacaoTransporteRegistrarCustoRealizadoRequest request,CancellationToken ct);Task<EducacaoTransporteRelatorioViagemDto> RelatorioViagemAsync(EducacaoAvancadaContext contexto,long viagemId,CancellationToken ct);} public interface IEducacaoTransporteDashboardService:IEducacaoTransporteEscolarService{} public interface IEducacaoTransporteRelatorioService:IEducacaoTransporteEscolarService{}
public interface IEducacaoMerendaRepository:IEducacaoAvancadaRepository{} public interface IEducacaoMerendaService:IEducacaoAvancadaRepository{Task<long> CriarCardapioComItensAsync(EducacaoAvancadaContext contexto,EducacaoMerendaCriarCardapioRequest request,CancellationToken ct); Task<long> RegistrarEstoqueAsync(EducacaoAvancadaContext contexto,EducacaoMerendaRegistrarEstoqueRequest request,CancellationToken ct); Task<long> DistribuirRefeicaoAsync(EducacaoAvancadaContext contexto,EducacaoMerendaDistribuirRefeicaoRequest request,CancellationToken ct); Task<long> LancarEstoqueAsync(EducacaoAvancadaContext contexto,EducacaoMerendaEstoqueLancarRequest request,CancellationToken ct); Task<long> EstornarMovimentoAsync(EducacaoAvancadaContext contexto,long movimentoId,EducacaoMerendaEstornoRequest request,CancellationToken ct); Task<long> EstornarDistribuicaoAsync(EducacaoAvancadaContext contexto,long distribuicaoId,EducacaoMerendaEstornoRequest request,CancellationToken ct);} public interface IEducacaoMerendaEstoqueService:IEducacaoMerendaService{} public interface IEducacaoMerendaCardapioService:IEducacaoMerendaService{} public interface IEducacaoMerendaRelatorioService:IEducacaoMerendaService{}
public interface IEducacaoBibliotecaRepository:IEducacaoAvancadaRepository{} public interface IEducacaoBibliotecaService:IEducacaoAvancadaRepository{Task<long> CriarEmprestimoAsync(EducacaoAvancadaContext contexto,EducacaoBibliotecaCriarEmprestimoRequest request,CancellationToken ct);Task<long> DevolverEmprestimoAsync(EducacaoAvancadaContext contexto,long emprestimoId,EducacaoBibliotecaDevolverRequest request,CancellationToken ct);Task<long> CriarReservaAsync(EducacaoAvancadaContext contexto,EducacaoBibliotecaCriarReservaRequest request,CancellationToken ct);Task<EducacaoBibliotecaDashboardDto> BibliotecaDashboardAsync(long tenantId,CancellationToken ct);Task<long> RenovarEmprestimoAsync(EducacaoAvancadaContext contexto,long emprestimoId,CancellationToken ct);Task<long> LancarOcorrenciaAsync(EducacaoAvancadaContext contexto,EducacaoBibliotecaOcorrenciaLancarRequest request,CancellationToken ct);Task<long> EstornarOcorrenciaAsync(EducacaoAvancadaContext contexto,long ocorrenciaId,EducacaoBibliotecaEstornoOcorrenciaRequest request,CancellationToken ct);Task<long> DesbloquearExemplarAsync(EducacaoAvancadaContext contexto,long exemplarId,EducacaoBibliotecaDesbloquearRequest request,CancellationToken ct);Task<long> CriarInventarioAsync(EducacaoAvancadaContext contexto,EducacaoBibliotecaInventarioLancarRequest request,CancellationToken ct);Task<long> ContarInventarioItemAsync(EducacaoAvancadaContext contexto,long inventarioId,EducacaoBibliotecaInventarioContarRequest request,CancellationToken ct);Task<long> JustificarInventarioItemAsync(EducacaoAvancadaContext contexto,long inventarioId,EducacaoBibliotecaInventarioJustificarRequest request,CancellationToken ct);Task<long> FecharInventarioAsync(EducacaoAvancadaContext contexto,long inventarioId,string? justificativa,CancellationToken ct);Task<List<EducacaoBibliotecaHistoricoItemDto>> HistoricoExemplarAsync(EducacaoAvancadaContext contexto,long exemplarId,CancellationToken ct);Task<List<EducacaoBibliotecaHistoricoItemDto>> HistoricoTomadorAsync(EducacaoAvancadaContext contexto,long? alunoId,long? servidorId,CancellationToken ct);} public interface IEducacaoBibliotecaEmprestimoService:IEducacaoBibliotecaService{} public interface IEducacaoBibliotecaRelatorioService:IEducacaoBibliotecaService{}
public interface IEducacaoIndicadoresRepository:IEducacaoAvancadaRepository{} public interface IEducacaoIndicadoresService:IEducacaoAvancadaRepository{Task<long> LancarParametroAsync(EducacaoAvancadaContext contexto,EducacaoFundebLancarParametroRequest request,CancellationToken ct);Task<long> LancarReceitaAsync(EducacaoAvancadaContext contexto,EducacaoFundebLancarReceitaRequest request,CancellationToken ct);Task<long> LancarAplicacaoAsync(EducacaoAvancadaContext contexto,EducacaoFundebLancarAplicacaoRequest request,CancellationToken ct);Task<long> LancarCentroCustoAsync(EducacaoAvancadaContext contexto,EducacaoCustoLancarCentroRequest request,CancellationToken ct);Task<EducacaoIndicadoresDashboardDto> IndicadoresDashboardAsync(long tenantId,CancellationToken ct);Task<long> LancarIndicadorAsync(EducacaoAvancadaContext contexto,EducacaoIndicadorLancarRequest request,CancellationToken ct);Task<long> LancarIndicadorValorAsync(EducacaoAvancadaContext contexto,EducacaoIndicadorValorLancarRequest request,CancellationToken ct);Task<long> LancarProjecaoMatriculaAsync(EducacaoAvancadaContext contexto,EducacaoProjecaoMatriculaLancarRequest request,CancellationToken ct);Task<long> LancarEducacensoLoteAsync(EducacaoAvancadaContext contexto,EducacaoEducacensoLoteLancarRequest request,CancellationToken ct);Task<long> LancarEducacensoInconsistenciaAsync(EducacaoAvancadaContext contexto,EducacaoEducacensoInconsistenciaLancarRequest request,CancellationToken ct);Task<EducacaoEducacensoDashboardDto> EducacensoDashboardAsync(long tenantId,CancellationToken ct);} public interface IEducacaoFundebService:IEducacaoIndicadoresService{} public interface IEducacaoCustosService:IEducacaoIndicadoresService{} public interface IEducacaoEducacensoService:IEducacaoIndicadoresService{} public interface IEducacaoAvaliacaoMagisterioService:IEducacaoIndicadoresService{}
