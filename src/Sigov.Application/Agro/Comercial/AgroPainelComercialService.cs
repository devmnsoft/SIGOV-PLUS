using Sigov.Application.Agro.Permissions;
using Sigov.Domain.Common;

namespace Sigov.Application.Agro.Comercial;

public sealed class AgroPainelComercialService : IAgroPainelComercialService
{
    private readonly IAgroAccessChecker _accessChecker; private readonly IAgroPainelComercialRepository _repository;
    public AgroPainelComercialService(IAgroAccessChecker accessChecker, IAgroPainelComercialRepository repository) { _accessChecker = accessChecker; _repository = repository; }
    public async Task<Result<AgroPainelComercialResponse>> ObterAsync(CancellationToken cancellationToken) { var c = await _accessChecker.CheckAsync(new AgroAccessRequest(AgroPermissions.ComercialVisualizar, "agro.painel_comercial"), cancellationToken).ConfigureAwait(false); if (c.IsFailure) return Result<AgroPainelComercialResponse>.Failure(c.Error!); var v = c.Value!; return Result<AgroPainelComercialResponse>.Success(await _repository.ObterAsync(v.TenantId, v.EntidadeId, cancellationToken).ConfigureAwait(false)); }
    public async Task<Result<AgroPainelComercialResponse>> AtualizarAsync(AgroPainelComercialConfigRequest request, CancellationToken cancellationToken) { var c = await _accessChecker.CheckAsync(new AgroAccessRequest(AgroPermissions.ComercialConfigurar, "agro.painel_comercial", request.EntidadeId), cancellationToken).ConfigureAwait(false); if (c.IsFailure) return Result<AgroPainelComercialResponse>.Failure(c.Error!); var v = c.Value!; return Result<AgroPainelComercialResponse>.Success(await _repository.AtualizarAsync(v.TenantId, request.EntidadeId ?? v.EntidadeId, request, cancellationToken).ConfigureAwait(false)); }
    public async Task<Result<AgroPainelComercialResponse>> ObterPublicoAsync(string tenantSlug, CancellationToken cancellationToken) { var response = await _repository.ObterPublicoAsync(tenantSlug, cancellationToken).ConfigureAwait(false); return response is null ? Result<AgroPainelComercialResponse>.Failure("Painel público não encontrado.") : Result<AgroPainelComercialResponse>.Success(response); }
}
