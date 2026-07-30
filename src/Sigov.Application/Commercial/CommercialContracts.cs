using Sigov.Application.Common;

namespace Sigov.Application.Commercial;

public sealed record ClienteFiltro(string? Busca = null, int Pagina = 1, int TamanhoPagina = 20, bool Inativos = false);
public sealed record ClienteResumoDto(Guid Id, string Nome, string? DocumentoMascarado, string? EmailMascarado, string? TelefoneMascarado, string Status, long Version);
public sealed record ClienteDetalheDto(Guid Id, string Nome, string? NomeFantasia, string TipoPessoa, string? Segmento, string? Origem, string? Documento, string? Email, string? Telefone, string Status, long Version);
public sealed record CriarClienteRequest(string Nome, string TipoPessoa, string? NomeFantasia, string? Segmento, string? Origem, string? Documento, string? Email, string? Telefone, Guid? ResponsavelUsuarioId);
public sealed record AtualizarClienteRequest(string Nome, string TipoPessoa, string? NomeFantasia, string? Segmento, string? Origem, string? Documento, string? Email, string? Telefone, Guid? ResponsavelUsuarioId, long Version);
public sealed record LeadResumoDto(Guid Id, string Nome, string Status, string? Origem, int Pontuacao, DateTimeOffset? ProximoContatoEm, long Version);
public sealed record LeadDetalheDto(Guid Id, string Nome, string Status, string? Origem, string? Interesse, int Pontuacao, DateTimeOffset? ProximoContatoEm, string? EmailMascarado, string? TelefoneMascarado, long Version);
public sealed record CriarLeadRequest(string Nome, string? Origem, string? Interesse, string? Documento, string? Email, string? Telefone, DateTimeOffset? ProximoContatoEm, Guid? ResponsavelUsuarioId);
public sealed record QualificarLeadRequest(int Pontuacao, string? Observacao, long Version);
public sealed record ConverterLeadRequest(Guid? ClienteId, string TituloOportunidade, decimal ValorEstimado, int Probabilidade, DateOnly? PrevisaoFechamento, long Version);
public sealed record ConversaoLeadDto(Guid ClienteId, Guid OportunidadeId);
public sealed record OportunidadeResumoDto(Guid Id, string Titulo, string Fase, string Cliente, decimal ValorEstimado, int Probabilidade, DateOnly? PrevisaoFechamento, long Version);
public sealed record OportunidadeDetalheDto(Guid Id, Guid ClienteId, Guid? LeadId, string Titulo, string Fase, decimal ValorEstimado, int Probabilidade, DateOnly? PrevisaoFechamento, string? MotivoPerda, long Version);
public sealed record CriarOportunidadeRequest(Guid ClienteId, Guid? LeadId, string Titulo, decimal ValorEstimado, int Probabilidade, DateOnly? PrevisaoFechamento, Guid? ResponsavelUsuarioId);
public sealed record AtualizarOportunidadeRequest(string Titulo, decimal ValorEstimado, int Probabilidade, DateOnly? PrevisaoFechamento, Guid? ResponsavelUsuarioId, long Version);
public sealed record MoverOportunidadeRequest(string Fase, string? Motivo, string? Observacao, long Version);
public sealed record PropostaItemRequest(Guid? ProdutoId, string Descricao, string Unidade, decimal Quantidade, decimal ValorUnitario, decimal Desconto, int Ordem);
public sealed record CriarPropostaRequest(Guid ClienteId, Guid? OportunidadeId, DateOnly ValidadeEm, decimal Desconto, decimal Acrescimo, string? CondicoesPagamento, string? Observacao, IReadOnlyList<PropostaItemRequest> Itens);
public sealed record AtualizarPropostaRequest(DateOnly ValidadeEm, decimal Desconto, decimal Acrescimo, string? CondicoesPagamento, string? Observacao, long Version);
public sealed record EmitirPropostaRequest(long Version);
public sealed record DecidirPropostaRequest(string? Motivo, long Version);
public sealed record PropostaResumoDto(Guid Id, string Numero, string Cliente, string Status, DateOnly ValidadeEm, decimal Total, long Version);
public sealed record PropostaDetalheDto(Guid Id, string Numero, Guid ClienteId, Guid? OportunidadeId, string Status, DateOnly ValidadeEm, decimal Subtotal, decimal Desconto, decimal Acrescimo, decimal Total, string? CondicoesPagamento, string? Observacao, long Version, IReadOnlyList<PropostaItemDto> Itens);
public sealed record PropostaItemDto(Guid Id, Guid? ProdutoId, string Descricao, string Unidade, decimal Quantidade, decimal ValorUnitario, decimal Desconto, decimal Total, int Ordem, long Version);
public sealed record PedidoResumoDto(Guid Id, string Numero, string Cliente, string Status, decimal Total, DateOnly? PrevisaoEntrega, long Version);
public sealed record PedidoDetalheDto(Guid Id, string Numero, Guid PropostaId, Guid ClienteId, string Status, decimal Subtotal, decimal Desconto, decimal Total, bool RequerOrdemServico, Guid? OrdemServicoId, long Version);
public sealed record ConfirmarPedidoRequest(long Version);
public sealed record CancelarPedidoRequest(string Motivo, long Version);
public sealed record GerarOrdemServicoRequest(string? Observacao, long Version);
public sealed record ComercialTimelineItemDto(DateTimeOffset Data, string Tipo, string Descricao, string? Usuario, string CorrelationId);
public sealed record ComercialDashboardDto(int LeadsNovos, int LeadsQualificados, decimal TaxaConversao, int OportunidadesAbertas, decimal Pipeline, decimal PipelinePonderado, int PropostasEmitidas, int PropostasAprovadas, decimal ValorAprovado, int PedidosConfirmados, decimal TicketMedio, IReadOnlyDictionary<string, int> Funil);

public interface ICommercialRepository
{
    Task<PagedResult<ClienteResumoDto>> ListarClientesAsync(Guid tenantId, ClienteFiltro filtro, CancellationToken ct);
    Task<ClienteDetalheDto?> ObterClienteAsync(Guid tenantId, Guid id, bool dadosPessoais, CancellationToken ct);
    Task<Guid> CriarClienteAsync(Guid tenantId, Guid usuarioId, CriarClienteRequest request, string correlationId, CancellationToken ct);
    Task<PagedResult<LeadResumoDto>> ListarLeadsAsync(Guid tenantId, int pagina, int tamanho, string? busca, CancellationToken ct);
    Task<Guid> CriarLeadAsync(Guid tenantId, Guid usuarioId, CriarLeadRequest request, string correlationId, CancellationToken ct);
    Task<ConversaoLeadDto> ConverterLeadAsync(Guid tenantId, Guid usuarioId, Guid id, ConverterLeadRequest request, string correlationId, CancellationToken ct);
    Task<PagedResult<OportunidadeResumoDto>> ListarOportunidadesAsync(Guid tenantId, int pagina, int tamanho, string? fase, string? busca, CancellationToken ct);
    Task MoverOportunidadeAsync(Guid tenantId, Guid usuarioId, Guid id, MoverOportunidadeRequest request, string correlationId, CancellationToken ct);
    Task<PagedResult<PropostaResumoDto>> ListarPropostasAsync(Guid tenantId, int pagina, int tamanho, CancellationToken ct);
    Task<PropostaDetalheDto?> ObterPropostaAsync(Guid tenantId, Guid id, CancellationToken ct);
    Task<Guid> CriarPropostaAsync(Guid tenantId, Guid usuarioId, CriarPropostaRequest request, string correlationId, CancellationToken ct);
    Task EmitirPropostaAsync(Guid tenantId, Guid usuarioId, Guid id, long version, string correlationId, CancellationToken ct);
    Task AprovarPropostaAsync(Guid tenantId, Guid usuarioId, Guid id, long version, string correlationId, CancellationToken ct);
    Task<PedidoDetalheDto> GerarPedidoAsync(Guid tenantId, Guid usuarioId, Guid propostaId, string idempotencyKey, string correlationId, CancellationToken ct);
    Task<PagedResult<PedidoResumoDto>> ListarPedidosAsync(Guid tenantId, int pagina, int tamanho, CancellationToken ct);
    Task ConfirmarPedidoAsync(Guid tenantId, Guid usuarioId, Guid id, long version, string correlationId, CancellationToken ct);
    Task<ComercialDashboardDto> ObterDashboardAsync(Guid tenantId, DateOnly inicio, DateOnly fim, CancellationToken ct);
}
