using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;
using Sigov.Domain.Common;

namespace Sigov.Application.Saas.WhiteLabel;

public sealed class TenantDominioService : ITenantDominioService
{
    private readonly ITenantDominioRepository _repository;
    private readonly TenantDominioValidator _validator;
    private readonly ILogger<TenantDominioService> _logger;

    public TenantDominioService(ITenantDominioRepository repository, TenantDominioValidator validator, ILogger<TenantDominioService> logger)
    {
        _repository = repository;
        _validator = validator;
        _logger = logger;
    }

    public Task<IReadOnlyCollection<TenantDominioResponse>> ListAsync(long tenantId, CancellationToken cancellationToken) => _repository.ListAsync(tenantId, cancellationToken);

    public async Task<Result<TenantDominioResponse>> CreateAsync(long tenantId, TenantDominioCreateRequest request, long usuarioId, CancellationToken cancellationToken)
    {
        var allowed = await _repository.PlanoPermiteDominioAsync(tenantId, cancellationToken).ConfigureAwait(false);
        var validation = _validator.Validate(tenantId, request, allowed);
        if (validation.IsFailure) return Result<TenantDominioResponse>.Failure(validation.Error ?? "Domínio inválido.");
        var correlationId = Guid.NewGuid();
        var tokenHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes($"{request.Dominio}:{correlationId}")));
        var created = await _repository.CreateAsync(tenantId, request.Dominio, tokenHash, usuarioId, correlationId, cancellationToken).ConfigureAwait(false);
        await _repository.InsertEventoAsync(tenantId, "TenantDominioSolicitado", "saas_tenant_dominio", created.Id, new { created.TenantId, created.Dominio }, correlationId, cancellationToken).ConfigureAwait(false);
        return Result<TenantDominioResponse>.Success(created);
    }

    public async Task<Result<TenantDominioResponse>> VerificarAsync(long tenantId, long id, VerificarDominioRequest request, long usuarioId, CancellationToken cancellationToken)
    {
        var correlationId = Guid.NewGuid();
        var verified = await _repository.VerifyAsync(tenantId, id, usuarioId, correlationId, cancellationToken).ConfigureAwait(false);
        await _repository.InsertEventoAsync(tenantId, "TenantDominioVerificado", "saas_tenant_dominio", id, new { tenantId, id }, correlationId, cancellationToken).ConfigureAwait(false);
        _logger.LogInformation("Domínio {DominioId} verificado estruturalmente para tenant {TenantId}.", id, tenantId);
        return Result<TenantDominioResponse>.Success(verified);
    }
}
