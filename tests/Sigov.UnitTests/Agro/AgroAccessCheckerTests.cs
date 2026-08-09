using FluentAssertions;
using Sigov.Application.Abstractions;
using Sigov.Application.Agro.Permissions;
using Sigov.Application.Saas.Modules;
using Sigov.Application.Saas.Profiles;
using Xunit;

namespace Sigov.UnitTests.Agro;

public sealed class AgroAccessCheckerTests
{
    [Fact]
    public async Task Modulo_Agro_Contratado_Permite_Acesso()
    {
        var checker = CriarChecker(moduleAllowed: true, permissions: new[] { AgroPermissions.DashboardVisualizar });

        var result = await checker.CheckAsync(new AgroAccessRequest(AgroPermissions.DashboardVisualizar, "agro.dashboard"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.TenantId.Should().Be(10);
    }

    [Fact]
    public async Task Modulo_Agro_Nao_Contratado_Bloqueia()
    {
        var checker = CriarChecker(moduleAllowed: false, permissions: new[] { AgroPermissions.DashboardVisualizar });

        var result = await checker.CheckAsync(new AgroAccessRequest(AgroPermissions.DashboardVisualizar, "agro.dashboard"), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("403");
    }

    [Fact]
    public async Task Permissao_Ausente_Bloqueia()
    {
        var checker = CriarChecker(moduleAllowed: true, permissions: Array.Empty<string>());

        var result = await checker.CheckAsync(new AgroAccessRequest(AgroPermissions.GeoCriar, "agro.geo"), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("403");
    }

    private static AgroAccessChecker CriarChecker(bool moduleAllowed, IReadOnlyCollection<string> permissions)
    {
        return new AgroAccessChecker(
            new TenantFake(10, null, null),
            new UserFake(99, true),
            new ModuleCheckerFake(moduleAllowed),
            new EffectivePermissionFake(permissions));
    }

    private sealed record TenantFake(long? TenantId, long? EntidadeId, long? ExercicioId) : ICurrentTenant
    {
        public string? TenantSlug => "tenant-agro";
    }

    private sealed record UserFake(long? UsuarioId, bool IsAuthenticated) : ICurrentUser
    {
        public string? Nome => "Usuário Agro";

        public long? UserId => throw new NotImplementedException();

        public long? TenantId => throw new NotImplementedException();

        public string? Login => throw new NotImplementedException();

        public string? Email => throw new NotImplementedException();

        public string? TenantName => throw new NotImplementedException();

        public IReadOnlyCollection<string> Roles => throw new NotImplementedException();

        public IReadOnlyCollection<string> Permissions => throw new NotImplementedException();
    }

    private sealed class ModuleCheckerFake : IModuleAccessChecker
    {
        private readonly bool _allowed;
        public ModuleCheckerFake(bool allowed) => _allowed = allowed;
        public Task<ModuleAccessResult> CheckModuleAsync(ModuleAccessRequest request, CancellationToken cancellationToken) => Task.FromResult(_allowed ? ModuleAccessResult.Allow() : ModuleAccessResult.Forbidden("Módulo não contratado."));
        public Task<ModuleAccessResult> CheckFeatureAsync(ModuleAccessRequest request, string featureCode, CancellationToken cancellationToken) => CheckModuleAsync(request, cancellationToken);
    }

    private sealed class EffectivePermissionFake : IEffectivePermissionService
    {
        private readonly IReadOnlyCollection<string> _permissions;
        public EffectivePermissionFake(IReadOnlyCollection<string> permissions) => _permissions = permissions;

        public Task<EffectivePermissionResult> CalculateAsync(long usuarioId, long? tenantId, CancellationToken cancellationToken)
        {
            var result = new EffectivePermissionResult(usuarioId, tenantId, false, new[] { "ADMINISTRADOR_TENANT" }, _permissions, Array.Empty<UserAccessScope>(), Array.Empty<string>());
            return Task.FromResult(result);
        }
    }
}
