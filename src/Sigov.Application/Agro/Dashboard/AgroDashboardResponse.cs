namespace Sigov.Application.Agro.Dashboard;

public sealed record AgroDashboardResponse(
    long TenantId,
    long? EntidadeId,
    long TotalCamadas,
    long TotalFeicoes,
    long TotalEventos,
    long TotalProdutores,
    long TotalPropriedades,
    long TotalVisitas,
    long TotalServicosMaquina,
    long TotalPontosCriticos,
    long ProdutoresAtivos = 0,
    decimal AreaTotalMapeada = 0,
    decimal AreaProdutiva = 0,
    long TotalTalhoes = 0,
    long CulturasCadastradas = 0,
    long SafrasAtivas = 0,
    decimal ProducaoEstimada = 0,
    decimal ProducaoRealizada = 0,
    long TotalProgramas = 0,
    long TotalBeneficios = 0,
    long BeneficiosConcedidosMes = 0,
    long TotalMaquinas = 0,
    long ServicosMaquinaPendentes = 0,
    long ServicosMaquinaExecutadosMes = 0,
    long ConcessoesSolicitadas = 0,
    long ConcessoesAutorizadas = 0,
    long ConcessoesEntreguesMes = 0,
    long InsumosDistribuidosMes = 0,
    long ServicosMaquinaAgendados = 0,
    decimal HorasTrabalhadasMes = 0,
    decimal AreaAtendidaMes = 0,
    long AlertasConflitoAgenda = 0);

public interface IAgroDashboardRepository
{
    Task<AgroDashboardResponse> ObterAsync(long tenantId, long? entidadeId, CancellationToken cancellationToken);
}
