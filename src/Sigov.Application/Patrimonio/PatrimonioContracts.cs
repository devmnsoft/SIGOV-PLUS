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
}

public sealed record PatrimonioBemFiltro(string? Busca = null, long? CategoriaId = null, string? Situacao = null, long? UnidadeId = null, long? ResponsavelUsuarioId = null, int Pagina = 1, int TamanhoPagina = 25);
public sealed record PatrimonioBemDto(long Id, string CodigoTombo, string Descricao, long? CategoriaId, string? Categoria, string TipoBem, string? Marca, string? Modelo, string? NumeroSerie, DateOnly? DataAquisicao, decimal? ValorAquisicao, decimal? ValorAtual, string EstadoConservacao, string Situacao, long? UnidadeId, long? SetorId, long? ResponsavelUsuarioId, string? Localizacao, string? Observacao, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt);
public sealed record PatrimonioBemInput(string CodigoTombo, string Descricao, long? CategoriaId, string TipoBem, string? CodigoAnterior, string? Marca, string? Modelo, string? NumeroSerie, DateOnly? DataAquisicao, decimal? ValorAquisicao, decimal? ValorAtual, string EstadoConservacao, long? UnidadeId, long? SetorId, long? ResponsavelUsuarioId, string? Localizacao, string? Observacao);
public sealed record PatrimonioMovimentacaoInput(long? UnidadeDestinoId, long? ResponsavelDestinoId, string? LocalizacaoDestino, string TipoMovimentacao, string Justificativa, DateTimeOffset? DataMovimentacao = null);
public sealed record PatrimonioBaixaInput(string TipoBaixa, string Justificativa, DateOnly DataBaixa, decimal? ValorBaixa);
public sealed record PatrimonioInventarioInput(string Codigo, string Descricao, long? UnidadeId, long? ResponsavelUsuarioId);
public sealed record PatrimonioConferenciaInput(bool Localizado, string? EstadoInformado, string? LocalizacaoInformada, string? DescricaoDivergencia, string? Observacao);
public sealed record PatrimonioInventarioDto(long Id, string Codigo, string Descricao, DateOnly DataAbertura, DateOnly? DataFechamento, string Situacao, long? UnidadeId, long? ResponsavelUsuarioId, int TotalItens, int Conferidos, int Divergencias);
public sealed record PatrimonioInventarioItemDto(long Id, long BemId, string CodigoTombo, string Descricao, bool? Localizado, string EstadoCadastro, string? EstadoInformado, string? LocalizacaoCadastro, string? LocalizacaoInformada, bool Divergencia, string? DescricaoDivergencia, DateTimeOffset? ConferidoEm);
public sealed record PatrimonioInventarioDetalhe(PatrimonioInventarioDto Inventario, IReadOnlyList<PatrimonioInventarioItemDto> Itens);
public sealed record PatrimonioDashboard(long TotalBens, IReadOnlyDictionary<string,long> PorSituacao, IReadOnlyDictionary<string,long> PorEstado, IReadOnlyList<PatrimonioAgrupamento> PorUnidade, long InventariosAbertos, long DivergenciasPendentes, long BaixasNoPeriodo);
public sealed record PatrimonioAgrupamento(string Nome, long Total);
public sealed record PatrimonioPagina<T>(IReadOnlyList<T> Itens, int Pagina, int TamanhoPagina, long Total);

public interface IPatrimonioService
{
    Task<PatrimonioPagina<PatrimonioBemDto>> ListarBensAsync(long tenantId, PatrimonioBemFiltro filtro, CancellationToken ct);
    Task<PatrimonioBemDto?> ObterBemAsync(long tenantId, long id, CancellationToken ct);
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
}
