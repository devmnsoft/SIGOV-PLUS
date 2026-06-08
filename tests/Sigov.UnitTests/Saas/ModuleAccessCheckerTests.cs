using FluentAssertions;
using Sigov.Application.Saas.Modules;
using Xunit;

namespace Sigov.UnitTests.Saas;

public sealed class ModuleAccessCheckerTests
{
    [Fact]
    public async Task Modulo_contratado_permite_acesso()
    {
        var checker = CreateChecker(new TenantModuleContract(1, "core", null, "HABILITADO", true));
        var result = await checker.CheckModuleAsync(new ModuleAccessRequest(1, "core", new[] { "ADMINISTRADOR_TENANT" }), CancellationToken.None);
        result.Allowed.Should().BeTrue();
    }

    [Fact]
    public async Task Modulo_nao_contratado_bloqueia()
    {
        var checker = CreateChecker();
        var result = await checker.CheckModuleAsync(new ModuleAccessRequest(1, "financeiro", new[] { "SERVIDOR" }), CancellationToken.None);
        result.StatusCode.Should().Be(403);
    }

    [Fact]
    public async Task Modulo_suspenso_bloqueia()
    {
        var checker = CreateChecker(new TenantModuleContract(1, "core", null, "SUSPENSO", true));
        var result = await checker.CheckModuleAsync(new ModuleAccessRequest(1, "core", new[] { "ADMINISTRADOR_TENANT" }), CancellationToken.None);
        result.Allowed.Should().BeFalse();
    }

    [Fact]
    public async Task Feature_desabilitada_bloqueia()
    {
        var checker = CreateChecker(new TenantModuleContract(1, "core", null, "HABILITADO", true));
        var result = await checker.CheckFeatureAsync(new ModuleAccessRequest(1, "core", new[] { "ADMINISTRADOR_TENANT" }), "core.operacao", CancellationToken.None);
        result.Allowed.Should().BeFalse();
    }

    private static ModuleAccessChecker CreateChecker(params TenantModuleContract[] contracts) => new(new ModuleCatalogService(), new FakeModuleAccessRepository(contracts));

    private sealed class FakeModuleAccessRepository : IModuleAccessRepository
    {
        private readonly IReadOnlyCollection<TenantModuleContract> _contracts;

        public FakeModuleAccessRepository(IReadOnlyCollection<TenantModuleContract> contracts) => _contracts = contracts;

        public Task<TenantModuleContract?> GetTenantModuleAsync(long tenantId, string moduleCode, CancellationToken cancellationToken) => Task.FromResult(_contracts.FirstOrDefault(item => item.TenantId == tenantId && string.Equals(item.ModuleCode, moduleCode, StringComparison.OrdinalIgnoreCase)));
        public Task<IReadOnlyCollection<TenantModuleContract>> GetTenantModulesAsync(long tenantId, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyCollection<TenantModuleContract>>(_contracts.Where(item => item.TenantId == tenantId).ToArray());
        public Task<bool> IsFeatureEnabledAsync(long tenantId, string moduleCode, string featureCode, CancellationToken cancellationToken) => Task.FromResult(false);
        public Task UpsertTenantModuleStatusAsync(long tenantId, string moduleCode, string status, long? userId, Guid? correlationId, CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
