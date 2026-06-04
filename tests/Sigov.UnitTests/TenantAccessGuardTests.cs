using FluentAssertions;
using Sigov.Application.Saas;
using Xunit;

namespace Sigov.UnitTests;

public sealed class TenantAccessGuardTests
{
    [Fact]
    public async Task EnsureTenantActiveAsync_DevePermitirTenantAtivo()
    {
        var guard = new TenantAccessGuard((_, _) => Task.FromResult<string?>("ATIVO"), new ModuleStub(true), new FeatureStub(true));

        (await guard.EnsureTenantActiveAsync(1, CancellationToken.None)).Should().BeTrue();
    }

    [Theory]
    [InlineData("SUSPENSO")]
    [InlineData("CANCELADO")]
    public async Task EnsureTenantActiveAsync_DeveBloquearTenantSemOperacao(string status)
    {
        var guard = new TenantAccessGuard((_, _) => Task.FromResult<string?>(status), new ModuleStub(true), new FeatureStub(true));

        (await guard.EnsureTenantActiveAsync(1, CancellationToken.None)).Should().BeFalse();
    }

    [Fact]
    public async Task EnsureModuleAsync_DeveBloquearModuloNaoContratado()
    {
        var guard = new TenantAccessGuard((_, _) => Task.FromResult<string?>("ATIVO"), new ModuleStub(false), new FeatureStub(true));

        (await guard.EnsureModuleAsync(1, "tributario", CancellationToken.None)).Should().BeFalse();
    }

    [Fact]
    public async Task EnsureFeatureAsync_DeveBloquearFeatureDesligada()
    {
        var guard = new TenantAccessGuard((_, _) => Task.FromResult<string?>("ATIVO"), new ModuleStub(true), new FeatureStub(false));

        (await guard.EnsureFeatureAsync(1, "tributario.nfse", CancellationToken.None)).Should().BeFalse();
    }

    private sealed class ModuleStub : IModuloLicenciamentoService
    {
        private readonly bool _enabled;
        public ModuleStub(bool enabled) => _enabled = enabled;
        public Task<bool> IsModuleEnabledAsync(long tenantId, string moduleCode, CancellationToken cancellationToken) => Task.FromResult(_enabled);
    }

    private sealed class FeatureStub : IFeatureFlagService
    {
        private readonly bool _enabled;
        public FeatureStub(bool enabled) => _enabled = enabled;
        public Task<bool> IsEnabledAsync(long tenantId, string featureCode, CancellationToken cancellationToken) => Task.FromResult(_enabled);
    }
}
