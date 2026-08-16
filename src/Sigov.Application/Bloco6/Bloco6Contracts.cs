using System.ComponentModel.DataAnnotations;

namespace Sigov.Application.Bloco6;

public sealed record Bloco6Context(Guid TenantId, Guid? EntidadeId, Guid? ExercicioId, string UsuarioId, string CorrelationId);
public sealed record Bloco6RegistroDto(Guid Id, string? Numero, string? Descricao, string Status, decimal? ValorTotal, DateTimeOffset CreatedAt);
public sealed record Bloco6DashboardDto(int Total, int Pendentes, decimal ValorTotal, IReadOnlyList<Bloco6RegistroDto> Recentes);
public sealed record ComprasFornecedorDto(Guid Id, string RazaoSocial, string DocumentoMascarado, bool Ativo);
public sealed record ComprasSolicitacaoItemDto([Required] string Descricao, [Required] string Unidade, [Range(0.0001,double.MaxValue)] decimal Quantidade, [Range(0,double.MaxValue)] decimal? ValorEstimado);
public sealed record ComprasCriarSolicitacaoRequest([Required] string Objeto, [MinLength(1)] IReadOnlyList<ComprasSolicitacaoItemDto> Itens);
public sealed record ComprasCriarFornecedorRequest([Required] string RazaoSocial, [Required] string Documento);
public sealed record ComprasCriarProcessoRequest([Required] string Objeto, [Required] string Modalidade, [Range(0,double.MaxValue)] decimal ValorEstimado);
public sealed record ComprasGerarOrdemCompraRequest(Guid ProcessoId, Guid FornecedorId, [MinLength(1)] IReadOnlyList<ComprasSolicitacaoItemDto> Itens, bool IntegrarFinanceiro);
public sealed record ContratoCriarRequest(Guid FornecedorId, [Required] string Objeto, [Range(0.01,double.MaxValue)] decimal Valor, DateOnly VigenciaInicio, DateOnly VigenciaFim);
public sealed record ContratoCriarAditivoRequest([Required] string Tipo, decimal? Valor, DateOnly? NovaVigenciaFim, [Required] string Justificativa);
public sealed record ContratoCriarMedicaoRequest([Range(0.01,double.MaxValue)] decimal Valor, [Required] string Descricao);
public sealed record AlmoxarifadoCriarItemRequest([Required] string Descricao, [Required] string Unidade, bool Patrimonializavel);
public sealed record AlmoxarifadoCriarMovimentoRequest(Guid AlmoxarifadoId, Guid ItemId, Guid? DestinoId, [Required] string Tipo, [Range(0.0001,double.MaxValue)] decimal Quantidade, string? Justificativa);
public sealed record PatrimonioCriarBemRequest([Required] string Tombamento, [Required] string Descricao, Guid LocalizacaoId, Guid? ResponsavelId, Guid? ContratoId);
public sealed record PatrimonioTransferirBemRequest(Guid LocalizacaoId, Guid? ResponsavelId, [Required] string Justificativa);
public sealed record PatrimonioBaixarBemRequest([Required] string Motivo);

public interface IBloco6Repository { Task<Bloco6DashboardDto> DashboardAsync(Bloco6Context context,string tabela,CancellationToken ct); }
public interface IComprasRepository:IBloco6Repository { Task<Guid> CriarSolicitacaoAsync(Bloco6Context context,ComprasCriarSolicitacaoRequest request,CancellationToken ct); Task<Guid> GerarOrdemAsync(Bloco6Context context,ComprasGerarOrdemCompraRequest request,CancellationToken ct); }
public interface IContratosRepository:IBloco6Repository { Task<Guid> CriarContratoAsync(Bloco6Context context,ContratoCriarRequest request,CancellationToken ct); Task<Guid> MedirAsync(Bloco6Context context,Guid contratoId,ContratoCriarMedicaoRequest request,CancellationToken ct); }
public interface IAlmoxarifadoRepository:IBloco6Repository { Task<Guid> MovimentarAsync(Bloco6Context context,AlmoxarifadoCriarMovimentoRequest request,CancellationToken ct); }
public interface IPatrimonioRepository:IBloco6Repository { Task<Guid> CriarBemAsync(Bloco6Context context,PatrimonioCriarBemRequest request,CancellationToken ct); }
public interface IComprasService { Task<Guid> CriarSolicitacaoAsync(Bloco6Context c,ComprasCriarSolicitacaoRequest r,CancellationToken ct); Task<Guid> GerarOrdemAsync(Bloco6Context c,ComprasGerarOrdemCompraRequest r,CancellationToken ct); }
public interface IComprasDashboardService { Task<Bloco6DashboardDto> ObterAsync(Bloco6Context c,CancellationToken ct); }
public interface IFornecedorService { string Normalizar(string documento); string Mascarar(string documento); }
public interface IComprasSolicitacaoService:IComprasService { }
public interface IComprasCotacaoService { }
public interface IComprasProcessoService { }
public interface IComprasOrdemCompraService:IComprasService { }
public interface IComprasIntegracaoFinanceiraService { }
public interface IComprasRelatorioService { }
public interface IContratosService { Task<Guid> CriarAsync(Bloco6Context c,ContratoCriarRequest r,CancellationToken ct); Task<Guid> MedirAsync(Bloco6Context c,Guid id,ContratoCriarMedicaoRequest r,CancellationToken ct); }
public interface IContratosDashboardService { Task<Bloco6DashboardDto> ObterAsync(Bloco6Context c,CancellationToken ct); }
public interface IContratoAditivoService { }
public interface IContratoMedicaoService:IContratosService { }
public interface IContratoAlertaService { }
public interface IContratoIntegracaoFinanceiraService { }
public interface IContratosRelatorioService { }
public interface IAlmoxarifadoService { Task<Guid> MovimentarAsync(Bloco6Context c,AlmoxarifadoCriarMovimentoRequest r,CancellationToken ct); }
public interface IAlmoxarifadoEstoqueService:IAlmoxarifadoService { }
public interface IAlmoxarifadoMovimentoService:IAlmoxarifadoService { }
public interface IAlmoxarifadoInventarioService { }
public interface IPatrimonioService { Task<Guid> CriarBemAsync(Bloco6Context c,PatrimonioCriarBemRequest r,CancellationToken ct); }
public interface IPatrimonioMovimentoService:IPatrimonioService { }
public interface IPatrimonioInventarioService { }
public interface IPatrimonioRelatorioService { }
