using Microsoft.Extensions.Logging;
using Sigov.Domain.Common;

namespace Sigov.Application.Saas.WhiteLabel;

public sealed class TenantBrandingService : ITenantBrandingService
{
    private readonly ITenantBrandingRepository _repository;
    private readonly TenantBrandingValidator _validator;
    private readonly ILogger<TenantBrandingService> _logger;

    public TenantBrandingService(ITenantBrandingRepository repository, TenantBrandingValidator validator, ILogger<TenantBrandingService> logger)
    {
        _repository = repository;
        _validator = validator;
        _logger = logger;
    }

    public async Task<TenantBrandingResponse> GetAsync(long tenantId, CancellationToken cancellationToken) =>
        await _repository.GetAsync(tenantId, cancellationToken).ConfigureAwait(false) ?? new TenantBrandingResponse(0, tenantId, "sigov", null, "#0d6efd", "#6c757d", "#198754", "SIGOV", null, null, false, await _repository.PlanoPermiteWhiteLabelAsync(tenantId, cancellationToken).ConfigureAwait(false), 240, 80, "contain", null, null, null, null);

    public async Task<Result<TenantBrandingResponse>> UpdateAsync(long tenantId, TenantBrandingUpdateRequest request, long usuarioId, CancellationToken cancellationToken)
    {
        var allowed = await _repository.PlanoPermiteWhiteLabelAsync(tenantId, cancellationToken).ConfigureAwait(false);
        var validation = _validator.Validate(tenantId, request, allowed);
        if (validation.IsFailure) return Result<TenantBrandingResponse>.Failure(validation.Error ?? "Branding inválido.");
        var correlationId = Guid.NewGuid();
        var updated = await _repository.UpsertAsync(tenantId, request, usuarioId, correlationId, cancellationToken).ConfigureAwait(false);
        await _repository.InsertEventoAsync(tenantId, "TenantBrandingAtualizado", "saas_tenant_branding", updated.Id, new { updated.TenantId, updated.WhiteLabelAtivo }, correlationId, cancellationToken).ConfigureAwait(false);
        _logger.LogInformation("Branding do tenant {TenantId} atualizado por {UsuarioId}.", tenantId, usuarioId);
        return Result<TenantBrandingResponse>.Success(updated);
    }
}
