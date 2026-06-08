using Microsoft.Extensions.Logging;
using Sigov.Domain.Common;

namespace Sigov.Application.Saas.Comercial;

public sealed class SaasAssinaturaService : ISaasAssinaturaService
{
    private readonly ISaasAssinaturaRepository _repository;
    private readonly SaasAssinaturaValidator _validator;
    private readonly ILogger<SaasAssinaturaService> _logger;

    public SaasAssinaturaService(ISaasAssinaturaRepository repository, SaasAssinaturaValidator validator, ILogger<SaasAssinaturaService> logger)
    {
        _repository = repository;
        _validator = validator;
        _logger = logger;
    }

    public Task<IReadOnlyCollection<SaasAssinaturaResponse>> ListAdminAsync(int page, int pageSize, CancellationToken cancellationToken) => _repository.ListAdminAsync((Math.Max(1, page) - 1) * Math.Clamp(pageSize, 1, 100), Math.Clamp(pageSize, 1, 100), cancellationToken);
    public Task<SaasAssinaturaResponse?> GetAdminAsync(long id, CancellationToken cancellationToken) => _repository.GetAdminAsync(id, cancellationToken);
    public Task<SaasAssinaturaResponse?> GetMinhaAssinaturaAsync(long tenantId, CancellationToken cancellationToken) => _repository.GetByTenantAsync(tenantId, cancellationToken);
    public Task<IReadOnlyCollection<string>> GetMeusModulosAsync(long tenantId, CancellationToken cancellationToken) => _repository.GetModulesByTenantAsync(tenantId, cancellationToken);
    public Task<IReadOnlyCollection<SaasPlanoLimiteResponse>> GetMeusLimitesAsync(long tenantId, CancellationToken cancellationToken) => _repository.GetLimitsByTenantAsync(tenantId, cancellationToken);

    public async Task<Result<SaasAssinaturaResponse>> UpdateAsync(long id, SaasAssinaturaUpdateRequest request, long usuarioId, CancellationToken cancellationToken)
    {
        var validation = _validator.ValidateUpdate(request);
        if (validation.IsFailure) return Result<SaasAssinaturaResponse>.Failure(validation.Error ?? "Assinatura inválida.");
        var correlationId = Guid.NewGuid();
        var updated = await _repository.UpdateAsync(id, request, usuarioId, correlationId, cancellationToken).ConfigureAwait(false);
        await _repository.InsertEventoAsync(updated.TenantId, "SaasAssinaturaAtualizada", "saas_assinatura", id, updated, correlationId, cancellationToken).ConfigureAwait(false);
        return Result<SaasAssinaturaResponse>.Success(updated);
    }

    public async Task<Result> ChangeStatusAsync(long id, string status, long usuarioId, CancellationToken cancellationToken)
    {
        var normalized = SaasAssinaturaMapper.NormalizeStatus(status);
        var correlationId = Guid.NewGuid();
        await _repository.ChangeStatusAsync(id, normalized, usuarioId, correlationId, cancellationToken).ConfigureAwait(false);
        await _repository.InsertEventoAsync(null, $"SaasAssinatura{normalized}", "saas_assinatura", id, new { id, status = normalized }, correlationId, cancellationToken).ConfigureAwait(false);
        _logger.LogInformation("Assinatura {AssinaturaId} alterada para {Status} por {UsuarioId}.", id, normalized, usuarioId);
        return Result.Success();
    }
}
