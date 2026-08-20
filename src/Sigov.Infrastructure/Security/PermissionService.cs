using Microsoft.Extensions.Logging;
using Sigov.Application.Abstractions;
using Sigov.Application.Authorization;

namespace Sigov.Infrastructure.Security;

/// <summary>Adapter legado: toda decisão é delegada ao avaliador central.</summary>
public sealed class PermissionService : IPermissionService
{
    private readonly IAuthorizationEvaluator _evaluator;
    private readonly ILogger<PermissionService> _logger;
    private readonly ICurrentTenant _tenant;
    public PermissionService(IAuthorizationEvaluator evaluator, ILogger<PermissionService> logger, ICurrentTenant tenant) => (_evaluator, _logger, _tenant) = (evaluator, logger, tenant);

    public async Task<bool> HasPermissionAsync(long usuarioId, string modulo, string recurso, string acao, CancellationToken cancellationToken = default)
    {
        if (!_tenant.TenantId.HasValue) return false;
        try
        {
            return (await _evaluator.EvaluateAsync(new AuthorizationRequest(usuarioId, recurso, acao, _tenant.TenantId, _tenant.EntidadeId, _tenant.ExercicioId), cancellationToken).ConfigureAwait(false)).Permitido;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro no adapter de permissão. UsuarioId={UsuarioId} Modulo={Modulo} Recurso={Recurso} Acao={Acao}", usuarioId, modulo, recurso, acao);
            throw;
        }
    }
}
