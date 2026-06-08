namespace Sigov.Application.Saas.Context;

public sealed class TenantContextSwitcher : ITenantContextSwitcher
{
    private readonly IGlobalAdminChecker _globalAdminChecker;
    private readonly ITenantContextSwitchRepository _repository;

    public TenantContextSwitcher(IGlobalAdminChecker globalAdminChecker, ITenantContextSwitchRepository repository)
    {
        _globalAdminChecker = globalAdminChecker;
        _repository = repository;
    }

    public async Task<TenantContextSwitchResult> SwitchAsync(TenantContextSwitchRequest request, CancellationToken cancellationToken)
    {
        if (!await _globalAdminChecker.IsGlobalAdminAsync(request.UsuarioGlobalId, cancellationToken).ConfigureAwait(false))
        {
            return new TenantContextSwitchResult(false, null, "Apenas ADMINISTRADOR_GERAL pode trocar contexto global.");
        }

        if (string.IsNullOrWhiteSpace(request.Motivo))
        {
            return new TenantContextSwitchResult(false, null, "Motivo é obrigatório para troca de contexto global.");
        }

        var logId = await _repository.StartSwitchAsync(request, cancellationToken).ConfigureAwait(false);
        return new TenantContextSwitchResult(true, logId, "Contexto global auditado iniciado.");
    }

    public async Task<TenantContextSwitchResult> FinishAsync(long logId, long usuarioGlobalId, CancellationToken cancellationToken)
    {
        if (!await _globalAdminChecker.IsGlobalAdminAsync(usuarioGlobalId, cancellationToken).ConfigureAwait(false))
        {
            return new TenantContextSwitchResult(false, null, "Apenas ADMINISTRADOR_GERAL pode finalizar contexto global.");
        }

        await _repository.FinishSwitchAsync(logId, usuarioGlobalId, cancellationToken).ConfigureAwait(false);
        return new TenantContextSwitchResult(true, logId, "Contexto global finalizado com auditoria.");
    }
}
