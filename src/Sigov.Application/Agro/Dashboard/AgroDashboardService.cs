using Microsoft.Extensions.Logging;
using Sigov.Application.Abstractions;
using Sigov.Application.Agro.Permissions;
using Sigov.Application.Saas;
using Sigov.Domain.Common;

namespace Sigov.Application.Agro.Dashboard;

public sealed class AgroDashboardService : IAgroDashboardService
{
    private readonly ICurrentTenant _tenant;
    private readonly ICurrentUser _user;
    private readonly IModuloLicenciamentoService _modulos;
    private readonly IPermissionService _permissoes;
    private readonly IAgroDashboardRepository _repository;
    private readonly ILogger<AgroDashboardService> _logger;

    public AgroDashboardService(ICurrentTenant tenant, ICurrentUser user, IModuloLicenciamentoService modulos, IPermissionService permissoes, IAgroDashboardRepository repository, ILogger<AgroDashboardService> logger)
    {
        _tenant = tenant;
        _user = user;
        _modulos = modulos;
        _permissoes = permissoes;
        _repository = repository;
        _logger = logger;
    }

    public async Task<Result<AgroDashboardResponse>> ObterAsync(CancellationToken cancellationToken)
    {
        var context = await ValidarContextoAsync("dashboard", "visualizar", cancellationToken).ConfigureAwait(false);
        if (context.IsFailure)
        {
            return Result<AgroDashboardResponse>.Failure(context.Error!);
        }

        var value = context.Value!;
        _logger.LogInformation("Obtendo dashboard Agro para tenant {TenantId}.", value.TenantId);
        return Result<AgroDashboardResponse>.Success(await _repository.ObterAsync(value.TenantId, value.EntidadeId, cancellationToken).ConfigureAwait(false));
    }

    private async Task<Result<(long TenantId, long? EntidadeId)>> ValidarContextoAsync(string recurso, string acao, CancellationToken cancellationToken)
    {
        if (!_tenant.TenantId.HasValue)
        {
            return Result<(long, long?)>.Failure("Tenant obrigatório.");
        }

        if (!await _modulos.IsModuleEnabledAsync(_tenant.TenantId.Value, AgroPermissions.Modulo, cancellationToken).ConfigureAwait(false))
        {
            return Result<(long, long?)>.Failure("Módulo Agro não contratado ou desabilitado para o tenant.");
        }

        if (!_user.IsAuthenticated)
        {
            return Result<(long, long?)>.Failure("Usuário não autenticado.");
        }

        if (_user.UsuarioId.HasValue && !await _permissoes.HasPermissionAsync(_user.UsuarioId.Value, AgroPermissions.Modulo, recurso, acao, cancellationToken).ConfigureAwait(false))
        {
            return Result<(long, long?)>.Failure("403");
        }

        return Result<(long, long?)>.Success((_tenant.TenantId.Value, _tenant.EntidadeId));
    }
}
