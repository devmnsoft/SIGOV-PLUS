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
    long TotalPontosCriticos);

public interface IAgroDashboardRepository
{
    Task<AgroDashboardResponse> ObterAsync(long tenantId, long? entidadeId, CancellationToken cancellationToken);
}
