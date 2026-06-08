using Sigov.Application.Abstractions;
using Sigov.Application.Saas.Modules;
using Sigov.Application.Saas.Profiles;
using Sigov.Domain.Common;
using Sigov.Domain.Saas;

namespace Sigov.Application.Agro.Permissions;

public sealed class AgroAccessChecker : IAgroAccessChecker
{
    private static readonly ISet<string> EscritaBloqueadaConsulta = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        AgroPermissions.GeoCriar,
        AgroPermissions.GeoEditar,
        AgroPermissions.GeoExcluir,
        AgroPermissions.ProdutorCriar,
        AgroPermissions.ProdutorEditar,
        AgroPermissions.ProdutorExcluir,
        AgroPermissions.PropriedadeCriar,
        AgroPermissions.PropriedadeEditar,
        AgroPermissions.PropriedadeExcluir,
        AgroPermissions.TalhaoCriar,
        AgroPermissions.TalhaoEditar,
        AgroPermissions.CulturaCriar,
        AgroPermissions.CulturaEditar,
        AgroPermissions.SafraCriar,
        AgroPermissions.SafraEditar,
        AgroPermissions.ProducaoCriar,
        AgroPermissions.ProducaoEditar,
        AgroPermissions.ProducaoExcluir,
        AgroPermissions.EstradaCriar,
        AgroPermissions.EstradaEditar,
        AgroPermissions.EstradaExcluir,
        AgroPermissions.PontoCriticoCriar,
        AgroPermissions.PontoCriticoEditar,
        AgroPermissions.PontoCriticoResolver,
        AgroPermissions.OcorrenciaCriar,
        AgroPermissions.OcorrenciaEditar,
        AgroPermissions.OcorrenciaResolver,
        AgroPermissions.OcorrenciaCancelar,
        AgroPermissions.ManutencaoCriar,
        AgroPermissions.ManutencaoExecutar,
        AgroPermissions.ManutencaoCancelar,
        AgroPermissions.FeiraCriar,
        AgroPermissions.FeiraEditar,
        AgroPermissions.FeiraExcluir,
        AgroPermissions.FeiranteCriar,
        AgroPermissions.FeiranteEditar,
        AgroPermissions.FeiranteAutorizar,
        AgroPermissions.FeiranteSuspender,
        AgroPermissions.AgroindustriaCriar,
        AgroPermissions.AgroindustriaEditar,
        AgroPermissions.AgroindustriaExcluir,
        AgroPermissions.InspecaoCriar,
        AgroPermissions.InspecaoConcluir,
        AgroPermissions.CompraAfCriar,
        AgroPermissions.CompraAfEditar,
        AgroPermissions.CompraAfCancelar,
        AgroPermissions.IndicadorGerenciar,
        AgroPermissions.RelatorioGerenciar,
        AgroPermissions.RelatorioExecutar,
        AgroPermissions.Exportar,
        AgroPermissions.TransparenciaGerenciar,
        AgroPermissions.TransparenciaPublicar,
        AgroPermissions.ComercialConfigurar
    };

    private static readonly ISet<string> PerfisComAcessoAoModulo = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        PerfilNivelCodigos.AdministradorGeral,
        PerfilNivelCodigos.AdministradorTenant,
        PerfilNivelCodigos.AdministradorEntidade,
        PerfilNivelCodigos.Coordenador,
        PerfilNivelCodigos.Diretor,
        PerfilNivelCodigos.Servidor,
        PerfilNivelCodigos.Operador,
        PerfilNivelCodigos.Consulta,
        PerfilNivelCodigos.Auditor,
        PerfilNivelCodigos.Suporte,
        "SIGOV_ADMIN",
        "SUPER_ADMIN"
    };

    private readonly ICurrentTenant _tenant;
    private readonly ICurrentUser _user;
    private readonly IModuleAccessChecker _moduleAccessChecker;
    private readonly IEffectivePermissionService _effectivePermissionService;

    public AgroAccessChecker(
        ICurrentTenant tenant,
        ICurrentUser user,
        IModuleAccessChecker moduleAccessChecker,
        IEffectivePermissionService effectivePermissionService)
    {
        _tenant = tenant;
        _user = user;
        _moduleAccessChecker = moduleAccessChecker;
        _effectivePermissionService = effectivePermissionService;
    }

    public async Task<Result<AgroAccessContext>> CheckAsync(AgroAccessRequest request, CancellationToken cancellationToken)
    {
        if (!_user.IsAuthenticated || !_user.UsuarioId.HasValue)
        {
            return Result<AgroAccessContext>.Failure("Usuário não autenticado.");
        }

        if (!_tenant.TenantId.HasValue)
        {
            return Result<AgroAccessContext>.Failure("Tenant obrigatório.");
        }

        var effective = await _effectivePermissionService.CalculateAsync(_user.UsuarioId.Value, _tenant.TenantId, cancellationToken).ConfigureAwait(false);
        if (!effective.ProfileCodes.Any(PerfisComAcessoAoModulo.Contains))
        {
            return Result<AgroAccessContext>.Failure("403");
        }

        var moduleRequest = new ModuleAccessRequest(_tenant.TenantId, AgroPermissions.Modulo, effective.ProfileCodes, HasTenantContext(effective));
        var moduleResult = string.IsNullOrWhiteSpace(request.FeatureFlag)
            ? await _moduleAccessChecker.CheckModuleAsync(moduleRequest, cancellationToken).ConfigureAwait(false)
            : await _moduleAccessChecker.CheckFeatureAsync(moduleRequest, request.FeatureFlag!, cancellationToken).ConfigureAwait(false);

        if (!moduleResult.Allowed)
        {
            return Result<AgroAccessContext>.Failure("403");
        }

        if (!effective.HasPermission(request.Permission))
        {
            return Result<AgroAccessContext>.Failure("403");
        }

        if (EscritaBloqueadaConsulta.Contains(request.Permission) && effective.ProfileCodes.Contains(PerfilNivelCodigos.Consulta, StringComparer.OrdinalIgnoreCase))
        {
            return Result<AgroAccessContext>.Failure("403");
        }

        var entidadeId = request.EntidadeId ?? _tenant.EntidadeId;
        var exercicioId = request.ExercicioId ?? _tenant.ExercicioId;
        if (!TemEscopo(effective, entidadeId, exercicioId))
        {
            return Result<AgroAccessContext>.Failure("403");
        }

        return Result<AgroAccessContext>.Success(new AgroAccessContext(_tenant.TenantId.Value, entidadeId, exercicioId, _user.UsuarioId.Value, effective.ProfileCodes, effective.Global));
    }

    private static bool HasTenantContext(EffectivePermissionResult effective) => !effective.Global || effective.TenantId.HasValue;

    private static bool TemEscopo(EffectivePermissionResult effective, long? entidadeId, long? exercicioId)
    {
        if (effective.Global || effective.ProfileCodes.Contains(PerfilNivelCodigos.AdministradorTenant, StringComparer.OrdinalIgnoreCase) || effective.ProfileCodes.Contains(PerfilNivelCodigos.Suporte, StringComparer.OrdinalIgnoreCase))
        {
            return true;
        }

        if (!entidadeId.HasValue && !exercicioId.HasValue)
        {
            return true;
        }

        return effective.Scopes.Any(scope =>
            (!entidadeId.HasValue || scope.EntidadeId == entidadeId || string.Equals(scope.Escopo, "TENANT", StringComparison.OrdinalIgnoreCase))
            && (!exercicioId.HasValue || scope.ExercicioId == exercicioId || scope.ExercicioId is null)
            && (string.IsNullOrWhiteSpace(scope.ModuloCodigo) || string.Equals(scope.ModuloCodigo, AgroPermissions.Modulo, StringComparison.OrdinalIgnoreCase)));
    }
}
