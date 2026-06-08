using Sigov.Domain.Saas;

namespace Sigov.Application.Saas.Profiles;

public sealed class EffectivePermissionService : IEffectivePermissionService
{
    private readonly IProfileLevelRepository _repository;

    public EffectivePermissionService(IProfileLevelRepository repository) => _repository = repository;

    public async Task<EffectivePermissionResult> CalculateAsync(long usuarioId, long? tenantId, CancellationToken cancellationToken)
    {
        var profiles = await _repository.GetUserProfileCodesAsync(usuarioId, cancellationToken).ConfigureAwait(false);
        var isGlobal = profiles.Any(PerfilNivelCodigos.GlobalAdminAliases.Contains);
        var permissions = isGlobal
            ? new[] { "saas.modulos.gerenciar", "saas.parametros.editar", "saas.perfis.gerenciar", "saas.contexto_global.trocar", "saas.contexto_global.visualizar_logs" }
            : await _repository.GetUserPermissionsAsync(usuarioId, tenantId, cancellationToken).ConfigureAwait(false);
        var scopes = isGlobal && tenantId is null
            ? Array.Empty<UserAccessScope>()
            : await _repository.GetUserScopesAsync(usuarioId, tenantId, cancellationToken).ConfigureAwait(false);
        var restrictions = new List<string>();

        if (!isGlobal && tenantId is null)
        {
            restrictions.Add("Usuário local sem tenant ativo não recebe permissões efetivas.");
        }

        if (!isGlobal && profiles.Contains(PerfilNivelCodigos.Consulta, StringComparer.OrdinalIgnoreCase))
        {
            restrictions.Add("Perfil CONSULTA restringe operações de escrita.");
        }

        if (!isGlobal && profiles.Contains(PerfilNivelCodigos.Servidor, StringComparer.OrdinalIgnoreCase))
        {
            restrictions.Add("Perfil SERVIDOR limitado às operações próprias e atribuídas.");
        }

        return new EffectivePermissionResult(usuarioId, tenantId, isGlobal, profiles, permissions.Distinct(StringComparer.OrdinalIgnoreCase).ToArray(), scopes, restrictions);
    }
}
