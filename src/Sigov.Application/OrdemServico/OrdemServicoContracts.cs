using Sigov.Application.Common;

namespace Sigov.Application.OrdemServico;

public sealed record OrdemServicoContext(Guid TenantId, Guid UsuarioId, string CorrelationId);
public sealed record OrdemServicoFiltro(int Pagina = 1, int Tamanho = 20, string? Status = null, string? Busca = null, Guid? TecnicoId = null);
public sealed record OrdemServicoResumoDto(Guid Id, string Numero, string Cliente, string Status, string Prioridade, DateTimeOffset? AgendadaInicio, Guid? TecnicoId, long Version);
public sealed record OrdemServicoItemDto(Guid Id, string Descricao, decimal Quantidade, string Unidade, bool Executado, string? Justificativa);
public sealed record OrdemServicoChecklistDto(Guid Id, string Titulo, string Tipo, bool Obrigatorio, bool Respondido, string? Resposta, bool BloqueiaConclusao, long Version);
public sealed record OrdemServicoApontamentoDto(Guid Id, Guid TecnicoId, string Atividade, DateTimeOffset Inicio, DateTimeOffset? Fim, int IntervaloMinutos);
public sealed record OrdemServicoConsumoDto(Guid Id, Guid ProdutoId, decimal Quantidade, decimal CustoUnitario, decimal QuantidadeDevolvida);
public sealed record OrdemServicoEvidenciaDto(Guid Id, string Tipo, string Nome, Guid DocumentoGedId, DateTimeOffset CriadoEm);
public sealed record OrdemServicoAceiteDto(Guid Id, string Nome, string DocumentoMascarado, bool Confirmado, string? Observacao, DateTimeOffset AceiteEm, string HashEvidencia);
public sealed record OrdemServicoCustoDto(decimal Horas, decimal CustoHoras, decimal CustoPecas, decimal Adicionais, decimal Descontos, decimal CustoTotal, decimal ValorCobrado, decimal Margem, decimal PercentualMargem);
public sealed record OrdemServicoHistoricoDto(string StatusAnterior, string StatusNovo, string? Observacao, DateTimeOffset CriadoEm, string CriadoPor);
public sealed record OrdemServicoDetalheDto(Guid Id, string Numero, Guid ClienteId, string Cliente, string Status, string Prioridade, string Origem, string Descricao, string? Endereco, Guid? PedidoId, Guid? PropostaId, Guid? TecnicoId, DateTimeOffset? AgendadaInicio, DateTimeOffset? AgendadaFim, DateTimeOffset? InicioReal, DateTimeOffset? ConclusaoEm, decimal CustoReal, long Version, IReadOnlyList<OrdemServicoItemDto> Itens);
public sealed record OrdemServicoAgendaDto(Guid Id, string Numero, string Cliente, string Prioridade, string Status, Guid? TecnicoId, DateTimeOffset Inicio, DateTimeOffset Fim, string? Endereco);
public sealed record TecnicoAgendaDto(Guid TecnicoId, string Tecnico, IReadOnlyList<OrdemServicoAgendaDto> Ordens);
public sealed record OrdemServicoDashboardDto(int Abertas, int Triagem, int Agendadas, int Execucao, int Pausadas, int AguardandoPeca, int Vencidas, int Concluidas, int Canceladas, decimal CustoMedio);
public sealed record CriarOrdemServicoRequest(Guid ClienteId, string Descricao, string Prioridade, string? Endereco, DateTimeOffset? PrazoSla);
public sealed record AtualizarOrdemServicoRequest(string Descricao, string Prioridade, string? Endereco, DateTimeOffset? PrazoSla, long Version);
public sealed record AgendarOrdemServicoRequest(Guid? TecnicoId, Guid? EquipeId, DateTimeOffset Inicio, DateTimeOffset Fim, string? Janela, string? Observacao, bool AutorizarConflito, string? Justificativa, long Version);
public sealed record AtribuirTecnicoRequest(Guid TecnicoId, Guid? EquipeId, long Version);
public sealed record IniciarOrdemServicoRequest(DateTimeOffset InicioReal, decimal? Latitude, decimal? Longitude, long Version);
public sealed record PausarOrdemServicoRequest(string Motivo, long Version);
public sealed record RetomarOrdemServicoRequest(long Version);
public sealed record ConcluirOrdemServicoRequest(string Diagnostico, string Solucao, string? JustificativaItemNaoExecutado, long Version);
public sealed record CancelarOrdemServicoRequest(string Motivo, long Version);
public sealed record ResponderChecklistRequest(Guid ItemId, string? Resposta, string? Observacao, Guid? EvidenciaId, long Version);
public sealed record RegistrarApontamentoRequest(Guid TecnicoId, string Atividade, DateTimeOffset Inicio, DateTimeOffset? Fim, int IntervaloMinutos, string? Observacao);
public sealed record ConsumirPecaRequest(Guid ProdutoId, Guid AlmoxarifadoId, decimal Quantidade, bool AutorizarSemReserva, bool AutorizarSaldoNegativo);
public sealed record DevolverPecaRequest(Guid ConsumoId, decimal Quantidade, string Motivo);
public sealed record RegistrarAceiteClienteRequest(string Nome, string DocumentoMascarado, bool Confirmado, string? Observacao, Guid? EvidenciaAssinaturaId);
public sealed record AdicionarEvidenciaRequest(Guid DocumentoGedId, string Tipo, string Nome);

public interface IOrdemServicoApplicationService
{
    Task<PagedResult<OrdemServicoResumoDto>> ListarAsync(OrdemServicoContext context, OrdemServicoFiltro filtro, CancellationToken ct);
    Task<OrdemServicoDetalheDto?> ObterAsync(OrdemServicoContext context, Guid id, CancellationToken ct);
    Task<Guid> CriarAsync(OrdemServicoContext context, CriarOrdemServicoRequest request, string idempotencyKey, CancellationToken ct);
    Task<Guid> GerarDoPedidoAsync(OrdemServicoContext context, Guid pedidoId, string idempotencyKey, CancellationToken ct);
    Task AgendarAsync(OrdemServicoContext context, Guid id, AgendarOrdemServicoRequest request, CancellationToken ct);
    Task AtribuirAsync(OrdemServicoContext context, Guid id, AtribuirTecnicoRequest request, CancellationToken ct);
    Task TransicionarAsync(OrdemServicoContext context, Guid id, string destino, long version, string? motivo, DateTimeOffset? inicioReal, CancellationToken ct);
    Task<IReadOnlyList<OrdemServicoAgendaDto>> AgendaAsync(OrdemServicoContext context, DateTimeOffset inicio, DateTimeOffset fim, Guid? tecnicoId, CancellationToken ct);
    Task<IReadOnlyList<OrdemServicoChecklistDto>> ChecklistAsync(OrdemServicoContext context, Guid id, CancellationToken ct);
    Task ResponderChecklistAsync(OrdemServicoContext context, Guid id, ResponderChecklistRequest request, CancellationToken ct);
    Task<IReadOnlyList<OrdemServicoApontamentoDto>> ApontamentosAsync(OrdemServicoContext context, Guid id, CancellationToken ct);
    Task RegistrarApontamentoAsync(OrdemServicoContext context, Guid id, RegistrarApontamentoRequest request, string key, CancellationToken ct);
    Task<IReadOnlyList<OrdemServicoConsumoDto>> PecasAsync(OrdemServicoContext context, Guid id, CancellationToken ct);
    Task ConsumirPecaAsync(OrdemServicoContext context, Guid id, ConsumirPecaRequest request, string key, CancellationToken ct);
    Task DevolverPecaAsync(OrdemServicoContext context, Guid id, DevolverPecaRequest request, string key, CancellationToken ct);
    Task<IReadOnlyList<OrdemServicoEvidenciaDto>> EvidenciasAsync(OrdemServicoContext context, Guid id, CancellationToken ct);
    Task AdicionarEvidenciaAsync(OrdemServicoContext context, Guid id, AdicionarEvidenciaRequest request, string key, CancellationToken ct);
    Task<OrdemServicoAceiteDto?> ObterAceiteAsync(OrdemServicoContext context, Guid id, CancellationToken ct);
    Task RegistrarAceiteAsync(OrdemServicoContext context, Guid id, RegistrarAceiteClienteRequest request, string key, CancellationToken ct);
    Task<OrdemServicoCustoDto> ObterCustosAsync(OrdemServicoContext context, Guid id, CancellationToken ct);
    Task<OrdemServicoDashboardDto> DashboardAsync(OrdemServicoContext context, CancellationToken ct);
}

public interface IOrdemServicoRepository : IOrdemServicoAgendaRepository, IOrdemServicoChecklistRepository, IOrdemServicoApontamentoRepository, IOrdemServicoConsumoRepository
{
    Task<PagedResult<OrdemServicoResumoDto>> ListarAsync(Guid tenantId, OrdemServicoFiltro filtro, CancellationToken ct); Task<OrdemServicoDetalheDto?> ObterAsync(Guid tenantId, Guid id, CancellationToken ct);
    Task<Guid> CriarAsync(Guid tenantId, Guid usuarioId, CriarOrdemServicoRequest request, string key, string correlationId, CancellationToken ct); Task<Guid> GerarDoPedidoAsync(Guid tenantId, Guid usuarioId, Guid pedidoId, string key, string correlationId, CancellationToken ct);
    Task AtribuirAsync(Guid tenantId, Guid usuarioId, Guid id, AtribuirTecnicoRequest request, string correlationId, CancellationToken ct); Task TransicionarAsync(Guid tenantId, Guid usuarioId, Guid id, string destino, long version, string? motivo, DateTimeOffset? inicioReal, string correlationId, CancellationToken ct); Task<OrdemServicoDashboardDto> DashboardAsync(Guid tenantId, CancellationToken ct);
    Task<IReadOnlyList<OrdemServicoEvidenciaDto>> EvidenciasAsync(Guid tenantId, Guid id, CancellationToken ct); Task AdicionarEvidenciaAsync(Guid tenantId, Guid usuarioId, Guid id, AdicionarEvidenciaRequest request, string key, string correlationId, CancellationToken ct);
    Task<OrdemServicoAceiteDto?> ObterAceiteAsync(Guid tenantId, Guid id, CancellationToken ct); Task RegistrarAceiteAsync(Guid tenantId, Guid usuarioId, Guid id, RegistrarAceiteClienteRequest request, string key, string correlationId, CancellationToken ct); Task<OrdemServicoCustoDto> ObterCustosAsync(Guid tenantId, Guid id, CancellationToken ct);
}
public interface IOrdemServicoAgendaRepository { Task AgendarAsync(Guid tenantId, Guid usuarioId, Guid id, AgendarOrdemServicoRequest request, string correlationId, CancellationToken ct); Task<IReadOnlyList<OrdemServicoAgendaDto>> AgendaAsync(Guid tenantId, DateTimeOffset inicio, DateTimeOffset fim, Guid? tecnicoId, CancellationToken ct); }
public interface IOrdemServicoChecklistRepository { Task<IReadOnlyList<OrdemServicoChecklistDto>> ChecklistAsync(Guid tenantId, Guid id, CancellationToken ct); Task ResponderChecklistAsync(Guid tenantId, Guid usuarioId, Guid id, ResponderChecklistRequest request, string correlationId, CancellationToken ct); }
public interface IOrdemServicoApontamentoRepository { Task<IReadOnlyList<OrdemServicoApontamentoDto>> ApontamentosAsync(Guid tenantId, Guid id, CancellationToken ct); Task RegistrarApontamentoAsync(Guid tenantId, Guid usuarioId, Guid id, RegistrarApontamentoRequest request, string key, string correlationId, CancellationToken ct); }
public interface IOrdemServicoConsumoRepository { Task<IReadOnlyList<OrdemServicoConsumoDto>> PecasAsync(Guid tenantId, Guid id, CancellationToken ct); Task ConsumirAsync(Guid tenantId, Guid usuarioId, Guid id, ConsumirPecaRequest request, string key, string correlationId, CancellationToken ct); Task DevolverAsync(Guid tenantId, Guid usuarioId, Guid id, DevolverPecaRequest request, string key, string correlationId, CancellationToken ct); }
