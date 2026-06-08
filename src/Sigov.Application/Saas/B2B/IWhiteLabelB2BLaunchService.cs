namespace Sigov.Application.Saas.B2B;

public interface IWhiteLabelB2BLaunchService
{
    Task<IReadOnlyCollection<B2BPlanoDto>> GetPlanosPublicosAsync(CancellationToken cancellationToken);
    Task<IReadOnlyCollection<B2BPlanoComparativoDto>> GetComparativoAsync(CancellationToken cancellationToken);
    Task<SelfServiceCadastroResult> SolicitarCadastroAsync(SelfServiceCadastroRequest request, string? ip, string? userAgent, CancellationToken cancellationToken);
    Task<WhiteLabelConfiguracaoDto> GetWhiteLabelAsync(long tenantId, CancellationToken cancellationToken);
    Task<WhiteLabelConfiguracaoDto> AtualizarWhiteLabelAsync(long tenantId, WhiteLabelAtualizarRequest request, long? usuarioId, CancellationToken cancellationToken);
    Task PublicarWhiteLabelAsync(long tenantId, long? usuarioId, CancellationToken cancellationToken);
    Task RestaurarWhiteLabelPadraoAsync(long tenantId, long? usuarioId, CancellationToken cancellationToken);
    Task<DeveloperOverviewDto> GetDeveloperOverviewAsync(long tenantId, CancellationToken cancellationToken);
    Task<ApiKeyCreateResult> CriarApiKeyAsync(long tenantId, ApiKeyCreateRequest request, long? usuarioId, CancellationToken cancellationToken);
    Task RevogarApiKeyAsync(long tenantId, long apiKeyId, long? usuarioId, CancellationToken cancellationToken);
    Task<AssinaturaUsoDto> GetUsoAssinaturaAsync(long tenantId, CancellationToken cancellationToken);
    Task<long> SolicitarUpgradeAsync(long tenantId, AssinaturaSolicitacaoRequest request, long? usuarioId, CancellationToken cancellationToken);
    Task<long> SolicitarDowngradeAsync(long tenantId, AssinaturaSolicitacaoRequest request, long? usuarioId, CancellationToken cancellationToken);
    Task<IReadOnlyCollection<ContratoSlaDto>> GetContratosAsync(long? tenantId, CancellationToken cancellationToken);
    Task<long> AbrirChamadoAsync(long tenantId, SuporteChamadoRequest request, long? usuarioId, CancellationToken cancellationToken);
    Task<IReadOnlyCollection<SuporteChamadoDto>> GetChamadosAsync(long tenantId, CancellationToken cancellationToken);
    Task<MonitoramentoB2BDto> GetMonitoramentoAsync(CancellationToken cancellationToken);
    Task<IReadOnlyCollection<GoToMarketMaterialDto>> GetMateriaisGoToMarketAsync(string visibilidade, CancellationToken cancellationToken);
    Task<IReadOnlyCollection<BetaFeedbackDto>> GetBetaFeedbacksAsync(long? tenantId, CancellationToken cancellationToken);
}
