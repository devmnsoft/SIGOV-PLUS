namespace Sigov.Application.Patrimonio;

public static class PatrimonioPermissoes
{
    public const string BemVisualizar = "patrimonio.bem.visualizar";
    public const string BemCriar = "patrimonio.bem.criar";
    public const string BemEditar = "patrimonio.bem.editar";
    public const string BemMovimentar = "patrimonio.bem.movimentar";
    public const string BemBaixar = "patrimonio.bem.baixar";
    public const string InventarioVisualizar = "patrimonio.inventario.visualizar";
    public const string InventarioCriar = "patrimonio.inventario.criar";
    public const string InventarioConferir = "patrimonio.inventario.conferir";
    public const string DashboardVisualizar = "patrimonio.dashboard.visualizar";
    public const string Exportar = "patrimonio.exportar";
    public const string IncorporacaoVisualizar = "patrimonio.incorporacao.visualizar";
    public const string IncorporacaoExecutar = "patrimonio.incorporacao.executar";
    public const string ResponsabilidadePropor = "patrimonio.responsabilidade.propor";
    public const string ResponsabilidadeAceitar = "patrimonio.responsabilidade.aceitar";
    public const string MovimentacaoOperar = "patrimonio.movimentacao.operar";
}

public sealed record PatrimonioBemFiltro(string? Busca = null, long? CategoriaId = null, string? Situacao = null, long? UnidadeId = null, long? ResponsavelUsuarioId = null, int Pagina = 1, int TamanhoPagina = 25);
public sealed record PatrimonioBemDto(long Id, string CodigoTombo, string Descricao, long? CategoriaId, string? Categoria, string TipoBem, string? Marca, string? Modelo, string? NumeroSerie, DateOnly? DataAquisicao, decimal? ValorAquisicao, decimal? ValorAtual, string EstadoConservacao, string Situacao, long? UnidadeId, long? SetorId, long? ResponsavelUsuarioId, string? Localizacao, string? Observacao, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt, string? UnidadeNome = null, string? ResponsavelNome = null);
public sealed record PatrimonioBemInput(string CodigoTombo, string Descricao, long? CategoriaId, string TipoBem, string? CodigoAnterior, string? Marca, string? Modelo, string? NumeroSerie, DateOnly? DataAquisicao, decimal? ValorAquisicao, decimal? ValorAtual, string EstadoConservacao, long? UnidadeId, long? SetorId, long? ResponsavelUsuarioId, string? Localizacao, string? Observacao);
public sealed record PatrimonioMovimentacaoDto(long Id, long BemId, long? UnidadeOrigemId, long? UnidadeDestinoId, long? ResponsavelOrigemId, long? ResponsavelDestinoId, string? LocalizacaoOrigem, string? LocalizacaoDestino, string TipoMovimentacao, string Justificativa, DateTimeOffset DataMovimentacao, long? UsuarioId);
public sealed record PatrimonioOpcaoDto(long Id, string Nome);
public sealed record PatrimonioRecebimentoElegivelDto(long ItemId,string Documento,string Fornecedor,string Produto,decimal QuantidadeRecebida,decimal QuantidadeAceita,long QuantidadeIncorporada,decimal SaldoElegivel,string Unidade,string? NumeroSerie);
public sealed record PatrimonioIncorporacaoInput(long RecebimentoItemId,int Quantidade,long CategoriaId,long UnidadeId,string Localizacao,string TipoBem,string EstadoConservacao,string? Marca,string? Modelo,string? Observacao,string CorrelationId);
public sealed record PatrimonioTermoDto(long Id,long BemId,long ResponsavelPropostoId,string ResponsavelProposto,string Status,string ConteudoSnapshot,string? MotivoRecusa,DateTimeOffset PropostoEm,DateTimeOffset? DecididoEm,DateTimeOffset? VigenciaInicio,DateTimeOffset? VigenciaFim);
public sealed record PatrimonioTransferenciaDto(long Id,long BemId,long? UnidadeOrigemId,string? UnidadeOrigem,long UnidadeDestinoId,string UnidadeDestino,string? LocalizacaoOrigem,string? LocalizacaoDestino,long? ResponsavelDestinoId,string Status,string Justificativa,string? MotivoRecusa,DateTimeOffset SolicitadaEm,DateTimeOffset? ExpedidaEm,DateTimeOffset? RecebidaEm,long Versao);
public sealed record PatrimonioManutencaoDto(long Id,string Status,string Prioridade,string Descricao,DateTimeOffset CreatedAt);
public sealed record PatrimonioBemDetalhe(PatrimonioBemDto Bem, IReadOnlyList<PatrimonioMovimentacaoDto> Movimentacoes,IReadOnlyList<PatrimonioTermoDto> Termos,IReadOnlyList<PatrimonioTransferenciaDto> Transferencias,IReadOnlyList<PatrimonioManutencaoDto> Manutencoes,string? DocumentoOrigem);
public sealed record PatrimonioTermoInput(long ResponsavelId,long? UnidadeId,string CorrelationId);
public sealed record PatrimonioTermoDecisaoInput(bool Aceitar,string? Motivo,long Versao=1);
public sealed record PatrimonioTransferenciaInput(long UnidadeDestinoId,string LocalizacaoDestino,long? ResponsavelDestinoId,string Justificativa,string CorrelationId);
public sealed record PatrimonioTransferenciaAcaoInput(string Acao,long Versao,string? Motivo);
public sealed record PatrimonioCadastroOpcoes(IReadOnlyList<PatrimonioOpcaoDto> Categorias,IReadOnlyList<PatrimonioOpcaoDto> Unidades,IReadOnlyList<PatrimonioOpcaoDto> Responsaveis);
public sealed record PatrimonioIncorporacoesPagina(IReadOnlyList<PatrimonioRecebimentoElegivelDto> Itens,PatrimonioCadastroOpcoes Opcoes);
public sealed record PatrimonioMovimentacaoInput(long? UnidadeDestinoId, long? ResponsavelDestinoId, string? LocalizacaoDestino, string TipoMovimentacao, string Justificativa, DateTimeOffset? DataMovimentacao = null);
public sealed record PatrimonioBaixaInput(string TipoBaixa, string Justificativa, DateOnly DataBaixa, decimal? ValorBaixa);
public sealed record PatrimonioInventarioInput(string Codigo, string Descricao, long? UnidadeId, long? ResponsavelUsuarioId);
public sealed record PatrimonioConferenciaInput(bool Localizado, string? EstadoInformado, string? LocalizacaoInformada, string? DescricaoDivergencia, string? Observacao);
public sealed record PatrimonioInventarioDto(long Id, string Codigo, string Descricao, DateOnly DataAbertura, DateOnly? DataFechamento, string Situacao, long? UnidadeId, long? ResponsavelUsuarioId, int TotalItens, int Conferidos, int Divergencias, string? UnidadeNome = null, string? ResponsavelNome = null);
public sealed record PatrimonioInventarioItemDto(long Id, long BemId, string CodigoTombo, string Descricao, bool? Localizado, string EstadoCadastro, string? EstadoInformado, string? LocalizacaoCadastro, string? LocalizacaoInformada, bool Divergencia, string? DescricaoDivergencia, DateTimeOffset? ConferidoEm);
public sealed record PatrimonioInventarioDetalhe(PatrimonioInventarioDto Inventario, IReadOnlyList<PatrimonioInventarioItemDto> Itens);
public sealed record PatrimonioDashboard(long TotalBens, IReadOnlyDictionary<string,long> PorSituacao, IReadOnlyDictionary<string,long> PorEstado, IReadOnlyList<PatrimonioAgrupamento> PorUnidade, long InventariosAbertos, long DivergenciasPendentes, long BaixasNoPeriodo);
public sealed record PatrimonioAgrupamento(string Nome, long Total);
public sealed record PatrimonioPagina<T>(IReadOnlyList<T> Itens, int Pagina, int TamanhoPagina, long Total);

public interface IPatrimonioService
{
    Task<PatrimonioPagina<PatrimonioBemDto>> ListarBensAsync(long tenantId, PatrimonioBemFiltro filtro, CancellationToken ct);
    Task<PatrimonioBemDto?> ObterBemAsync(long tenantId, long id, CancellationToken ct);
    Task<PatrimonioBemDetalhe?> ObterBemDetalheAsync(long tenantId, long id, CancellationToken ct);
    Task<long> CriarBemAsync(long tenantId, long usuarioId, string correlationId, PatrimonioBemInput input, CancellationToken ct);
    Task EditarBemAsync(long tenantId, long usuarioId, string correlationId, long id, PatrimonioBemInput input, CancellationToken ct);
    Task MovimentarBemAsync(long tenantId, long usuarioId, string correlationId, long id, PatrimonioMovimentacaoInput input, CancellationToken ct);
    Task BaixarBemAsync(long tenantId, long usuarioId, string correlationId, long id, PatrimonioBaixaInput input, CancellationToken ct);
    Task<PatrimonioPagina<PatrimonioInventarioDto>> ListarInventariosAsync(long tenantId, int pagina, int tamanho, CancellationToken ct);
    Task<PatrimonioInventarioDetalhe?> ObterInventarioAsync(long tenantId, long id, CancellationToken ct);
    Task<long> AbrirInventarioAsync(long tenantId, long usuarioId, string correlationId, PatrimonioInventarioInput input, CancellationToken ct);
    Task ConferirItemAsync(long tenantId, long usuarioId, string correlationId, long inventarioId, long itemId, PatrimonioConferenciaInput input, CancellationToken ct);
    Task FecharInventarioAsync(long tenantId, long usuarioId, string correlationId, long id, CancellationToken ct);
    Task<PatrimonioDashboard> ObterDashboardAsync(long tenantId, CancellationToken ct);
    Task<byte[]> ExportarCsvAsync(long tenantId, PatrimonioBemFiltro filtro, CancellationToken ct);
    Task<IReadOnlyList<PatrimonioRecebimentoElegivelDto>> ListarRecebimentosElegiveisAsync(long tenantId,long entidadeId,CancellationToken ct);
    Task<IReadOnlyList<long>> IncorporarAsync(long tenantId,long entidadeId,long usuarioId,PatrimonioIncorporacaoInput input,CancellationToken ct);
    Task<PatrimonioCadastroOpcoes> ObterOpcoesAsync(long tenantId,long entidadeId,CancellationToken ct);
    Task<long> ProporResponsabilidadeAsync(long tenantId,long entidadeId,long usuarioId,long bemId,PatrimonioTermoInput input,CancellationToken ct);
    Task DecidirResponsabilidadeAsync(long tenantId,long entidadeId,long usuarioId,long termoId,PatrimonioTermoDecisaoInput input,CancellationToken ct);
    Task<long> SolicitarTransferenciaAsync(long tenantId,long entidadeId,long usuarioId,long bemId,PatrimonioTransferenciaInput input,CancellationToken ct);
    Task OperarTransferenciaAsync(long tenantId,long entidadeId,long usuarioId,long transferenciaId,PatrimonioTransferenciaAcaoInput input,CancellationToken ct);
    Task<PatrimonioTermoDto?> ObterTermoAsync(long tenantId,long entidadeId,long termoId,CancellationToken ct);
}
