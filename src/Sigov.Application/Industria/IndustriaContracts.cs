namespace Sigov.Application.Industria;

public interface IIndustriaEstoqueService
{
    Task<IndustriaEstoqueResultado> ReservarMaterialAsync(long tenantId, long ordemId, long produtoId, decimal quantidade, string correlationId, CancellationToken cancellationToken = default);
    Task<IndustriaEstoqueResultado> ConsumirMaterialAsync(long tenantId, long ordemId, long produtoId, long? almoxarifadoId, decimal quantidade, long? usuarioId, string correlationId, CancellationToken cancellationToken = default);
    Task<IndustriaEstoqueResultado> EstornarConsumoAsync(long tenantId, long ordemId, long produtoId, decimal quantidade, string correlationId, CancellationToken cancellationToken = default);
    Task<IndustriaEstoqueResultado> RegistrarProdutoAcabadoAsync(long tenantId, long ordemId, long produtoId, long? almoxarifadoId, decimal quantidade, string? lote, DateTime? validade, long? usuarioId, string correlationId, CancellationToken cancellationToken = default);
    Task<IndustriaDisponibilidadeResultado> VerificarDisponibilidadeFichaAsync(long tenantId, long fichaTecnicaId, decimal quantidade, CancellationToken cancellationToken = default);
    Task<decimal> ObterCustoMedioAsync(long tenantId, long produtoId, CancellationToken cancellationToken = default);
}

public interface IIndustriaComercialService
{
    Task<long> GerarOrdemProducaoDoPedidoAsync(long tenantId, long pedidoId, long? usuarioId, string correlationId, CancellationToken cancellationToken = default);
}

public sealed record IndustriaEstoqueResultado(bool EstoqueAtivo, bool MovimentoRegistrado, string Mensagem);
public sealed record IndustriaDisponibilidadeResultado(bool EstoqueAtivo, bool Disponivel, IReadOnlyCollection<string> Alertas);
