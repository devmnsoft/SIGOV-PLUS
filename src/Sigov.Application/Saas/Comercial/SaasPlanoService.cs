using Microsoft.Extensions.Logging;
using Sigov.Domain.Common;

namespace Sigov.Application.Saas.Comercial;

public sealed class SaasPlanoService : ISaasPlanoService
{
    private readonly ISaasPlanoRepository _repository;
    private readonly SaasPlanoValidator _validator;
    private readonly ILogger<SaasPlanoService> _logger;

    public SaasPlanoService(ISaasPlanoRepository repository, SaasPlanoValidator validator, ILogger<SaasPlanoService> logger)
    {
        _repository = repository;
        _validator = validator;
        _logger = logger;
    }

    public Task<IReadOnlyCollection<SaasPlanoResponse>> ListPublicAsync(CancellationToken cancellationToken) => _repository.ListPublicAsync(cancellationToken);
    public Task<IReadOnlyCollection<SaasPlanoResponse>> ListAdminAsync(int page, int pageSize, CancellationToken cancellationToken) => _repository.ListAdminAsync((SaasPlanoMapper.NormalizePage(page) - 1) * SaasPlanoMapper.NormalizePageSize(pageSize), SaasPlanoMapper.NormalizePageSize(pageSize), cancellationToken);
    public Task<SaasPlanoDetalheResponse?> GetByCodigoAsync(string codigo, CancellationToken cancellationToken) => _repository.GetByCodigoAsync(codigo, cancellationToken);

    public async Task<Result<SaasPlanoResponse>> CreateAsync(SaasPlanoCreateRequest request, long usuarioId, CancellationToken cancellationToken)
    {
        var validation = _validator.ValidateCreate(request);
        if (validation.IsFailure) return Result<SaasPlanoResponse>.ValidationFailure(validation.ValidationErrors);
        var correlationId = Guid.NewGuid();
        var created = await _repository.CreateAsync(request, usuarioId, correlationId, cancellationToken).ConfigureAwait(false);
        await _repository.InsertEventoAsync(null, "SaasPlanoCriado", "saas_plano", created.Id, created, correlationId, cancellationToken).ConfigureAwait(false);
        _logger.LogInformation("Plano SaaS {Codigo} criado por {UsuarioId}.", created.Codigo, usuarioId);
        return Result<SaasPlanoResponse>.Success(created);
    }

    public async Task<Result<SaasPlanoResponse>> UpdateAsync(long id, SaasPlanoUpdateRequest request, long usuarioId, CancellationToken cancellationToken)
    {
        var validation = _validator.ValidateUpdate(request);
        if (validation.IsFailure) return Result<SaasPlanoResponse>.ValidationFailure(validation.ValidationErrors);
        var correlationId = Guid.NewGuid();
        var updated = await _repository.UpdateAsync(id, request, usuarioId, correlationId, cancellationToken).ConfigureAwait(false);
        await _repository.InsertEventoAsync(null, "SaasPlanoAtualizado", "saas_plano", updated.Id, updated, correlationId, cancellationToken).ConfigureAwait(false);
        return Result<SaasPlanoResponse>.Success(updated);
    }
}
