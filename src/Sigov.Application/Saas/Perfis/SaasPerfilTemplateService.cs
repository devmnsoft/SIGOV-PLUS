using Microsoft.Extensions.Logging;
using Sigov.Domain.Common;

namespace Sigov.Application.Saas.Perfis;

public sealed class SaasPerfilTemplateService : ISaasPerfilTemplateService
{
    private readonly ISaasPerfilTemplateRepository _repository;
    private readonly SaasPerfilTemplateValidator _validator;
    private readonly ILogger<SaasPerfilTemplateService> _logger;

    public SaasPerfilTemplateService(ISaasPerfilTemplateRepository repository, SaasPerfilTemplateValidator validator, ILogger<SaasPerfilTemplateService> logger)
    {
        _repository = repository;
        _validator = validator;
        _logger = logger;
    }

    public Task<IReadOnlyCollection<SaasPerfilTemplateResponse>> ListAsync(int page, int pageSize, CancellationToken cancellationToken) => _repository.ListAsync((Math.Max(1, page) - 1) * Math.Clamp(pageSize, 1, 100), Math.Clamp(pageSize, 1, 100), cancellationToken);

    public async Task<Result<SaasPerfilTemplateResponse>> CreateAsync(SaasPerfilTemplateResponse request, long usuarioId, CancellationToken cancellationToken)
    {
        var validation = _validator.Validate(request);
        if (validation.IsFailure) return Result<SaasPerfilTemplateResponse>.Failure(validation.Error ?? "Template inválido.");
        var correlationId = Guid.NewGuid();
        var created = await _repository.CreateAsync(request, usuarioId, correlationId, cancellationToken).ConfigureAwait(false);
        await _repository.InsertEventoAsync(null, "SaasPerfilTemplateCriado", "saas_perfil_template", created.Id, created, correlationId, cancellationToken).ConfigureAwait(false);
        return Result<SaasPerfilTemplateResponse>.Success(created);
    }

    public async Task<Result> CriarPerfisTenantPorTemplateAsync(CriarPerfisTenantPorTemplateRequest request, long usuarioId, CancellationToken cancellationToken)
    {
        var correlationId = Guid.NewGuid();
        await _repository.CriarPerfisTenantPorTemplateAsync(request, usuarioId, correlationId, cancellationToken).ConfigureAwait(false);
        await _repository.InsertEventoAsync(request.TenantId, "PerfisTenantCriados", "perfil_acesso", request.TenantId, request, correlationId, cancellationToken).ConfigureAwait(false);
        _logger.LogInformation("Perfis do tenant {TenantId} criados por template.", request.TenantId);
        return Result.Success();
    }
}
