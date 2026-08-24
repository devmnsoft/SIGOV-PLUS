namespace Sigov.Application.Almoxarifado;

public static class AlmoxarifadoPermissoes
{
    public const string DashboardVisualizar="almoxarifado.dashboard.visualizar"; public const string MaterialVisualizar="almoxarifado.material.visualizar";
    public const string MaterialCriar="almoxarifado.material.criar"; public const string MaterialEditar="almoxarifado.material.editar";
    public const string EstoqueVisualizar="almoxarifado.estoque.visualizar"; public const string Entrada="almoxarifado.movimentacao.entrada";
    public const string Saida="almoxarifado.movimentacao.saida"; public const string RequisicaoVisualizar="almoxarifado.requisicao.visualizar";
    public const string RequisicaoCriar="almoxarifado.requisicao.criar"; public const string RequisicaoAprovar="almoxarifado.requisicao.aprovar";
    public const string RequisicaoAtender="almoxarifado.requisicao.atender"; public const string Exportar="almoxarifado.exportar";
}
public sealed record AlmoxarifadoFiltro(string? Busca=null,bool? Ativo=null,int Pagina=1,int TamanhoPagina=25);
public sealed record MaterialDto(long Id,long EntidadeId,string Codigo,string Descricao,string TipoMaterial,string UnidadeMedida,string? Categoria,decimal EstoqueMinimo,decimal? EstoqueMaximo,bool ControlaLote,bool ControlaValidade,bool Ativo,DateTimeOffset CreatedAt,DateTimeOffset UpdatedAt);
public sealed record MaterialInput(long EntidadeId,string Codigo,string Descricao,string TipoMaterial,string UnidadeMedida,string? Categoria,decimal EstoqueMinimo,decimal? EstoqueMaximo,bool ControlaLote,bool ControlaValidade,bool Ativo=true);
public sealed record LocalDto(long Id,long EntidadeId,long? UnidadeId,string Codigo,string Nome,string? ResponsavelNome,bool Ativo);
public sealed record LocalInput(long EntidadeId,long? UnidadeId,string Codigo,string Nome,string? ResponsavelNome,bool Ativo=true);
public sealed record EstoqueDto(long LocalId,string LocalCodigo,string LocalNome,long MaterialId,string MaterialCodigo,string MaterialDescricao,string UnidadeMedida,decimal Quantidade,decimal EstoqueMinimo,bool AbaixoMinimo);
public sealed record MovimentacaoInput(long EntidadeId,long AlmoxarifadoId,long MaterialId,decimal Quantidade,string Tipo,string? DocumentoOrigem,string? Observacao,decimal? ValorUnitario=null,string? Lote=null,DateOnly? Validade=null);
public sealed record MovimentacaoDto(long Id,string Tipo,string Motivo,string MaterialCodigo,string MaterialDescricao,string LocalNome,decimal Quantidade,decimal SaldoAntes,decimal SaldoDepois,DateTimeOffset OcorridoEm);
public sealed record RequisicaoItemInput(long MaterialId,decimal QuantidadeSolicitada);
public sealed record RequisicaoInput(long EntidadeId,long AlmoxarifadoId,long? UnidadeSolicitanteId,string? Observacao,IReadOnlyList<RequisicaoItemInput> Itens);
public sealed record RequisicaoDto(long Id,long EntidadeId,long AlmoxarifadoId,string LocalNome,long? UnidadeSolicitanteId,string Status,string? Observacao,string? Justificativa,DateTimeOffset CreatedAt,int TotalItens);
public sealed record RequisicaoItemDto(long Id,long MaterialId,string Codigo,string Descricao,string UnidadeMedida,decimal QuantidadeSolicitada,decimal QuantidadeAtendida);
public sealed record RequisicaoHistoricoDto(string StatusAnterior,string StatusNovo,string? Justificativa,DateTimeOffset OcorridoEm);
public sealed record RequisicaoDetalhe(RequisicaoDto Requisicao,IReadOnlyList<RequisicaoItemDto> Itens,IReadOnlyList<RequisicaoHistoricoDto> Historico);
public sealed record BaixoEstoqueDto(string MaterialCodigo,string MaterialDescricao,string LocalNome,decimal Quantidade,decimal EstoqueMinimo);
public sealed record AlmoxarifadoDashboard(long MateriaisAtivos,long AbaixoMinimo,long RequisicoesPendentes,long EntradasMes,long SaidasMes,long PendenciasPatrimoniais,IReadOnlyList<MovimentacaoDto> UltimosMovimentos,IReadOnlyList<BaixoEstoqueDto> BaixoEstoque);
public sealed record AlmoxarifadoPagina<T>(IReadOnlyList<T> Itens,int Pagina,int TamanhoPagina,long Total);

public interface IAlmoxarifadoService
{
 Task<AlmoxarifadoDashboard> ObterDashboardAsync(long tenantId,long entidadeId,CancellationToken ct);
 Task<AlmoxarifadoPagina<MaterialDto>> ListarMateriaisAsync(long tenantId,long entidadeId,AlmoxarifadoFiltro filtro,CancellationToken ct); Task<MaterialDto?> ObterMaterialAsync(long tenantId,long entidadeId,long id,CancellationToken ct); Task<long> CriarMaterialAsync(long tenantId,long usuarioId,string correlationId,MaterialInput input,CancellationToken ct); Task EditarMaterialAsync(long tenantId,long usuarioId,string correlationId,long id,MaterialInput input,CancellationToken ct);
 Task<IReadOnlyList<LocalDto>> ListarLocaisAsync(long tenantId,long entidadeId,bool? ativo,CancellationToken ct); Task<long> CriarLocalAsync(long tenantId,long usuarioId,string correlationId,LocalInput input,CancellationToken ct);
 Task<IReadOnlyList<EstoqueDto>> ListarEstoqueAsync(long tenantId,long entidadeId,string? busca,CancellationToken ct); Task RegistrarEntradaAsync(long tenantId,long usuarioId,string correlationId,MovimentacaoInput input,CancellationToken ct); Task RegistrarSaidaAsync(long tenantId,long usuarioId,string correlationId,MovimentacaoInput input,CancellationToken ct);
 Task<AlmoxarifadoPagina<RequisicaoDto>> ListarRequisicoesAsync(long tenantId,long entidadeId,string? status,int pagina,CancellationToken ct); Task<RequisicaoDetalhe?> ObterRequisicaoAsync(long tenantId,long entidadeId,long id,CancellationToken ct); Task<long> CriarRequisicaoAsync(long tenantId,long usuarioId,string correlationId,RequisicaoInput input,CancellationToken ct); Task AlterarStatusAsync(long tenantId,long entidadeId,long usuarioId,string correlationId,long id,string acao,string? justificativa,CancellationToken ct);
 Task<byte[]> ExportarCsvAsync(long tenantId,long entidadeId,string tipo,long usuarioId,string correlationId,CancellationToken ct);
}
