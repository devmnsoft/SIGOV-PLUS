using FluentAssertions;
using Sigov.Application.Saas.Context;
using Xunit;

namespace Sigov.UnitTests.Saas;

public sealed class TenantContextSwitcherTests
{
    [Fact]
    public async Task Troca_de_contexto_exige_motivo()
    {
        var repository = new FakeSwitchRepository(new[] { "ADMINISTRADOR_GERAL" });
        var switcher = new TenantContextSwitcher(new GlobalAdminChecker(repository), repository);
        var result = await switcher.SwitchAsync(new TenantContextSwitchRequest(1, 2, null, "", null, null, null), CancellationToken.None);
        result.Success.Should().BeFalse();
    }

    [Fact]
    public async Task Troca_de_contexto_registra_log()
    {
        var repository = new FakeSwitchRepository(new[] { "ADMINISTRADOR_GERAL" });
        var switcher = new TenantContextSwitcher(new GlobalAdminChecker(repository), repository);
        var result = await switcher.SwitchAsync(new TenantContextSwitchRequest(1, 2, null, "Suporte auditado", "127.0.0.1", "tests", Guid.NewGuid()), CancellationToken.None);
        result.Success.Should().BeTrue();
        repository.Started.Should().BeTrue();
    }

    private sealed class FakeSwitchRepository : ITenantContextSwitchRepository
    {
        private readonly IReadOnlyCollection<string> _profiles;
        public FakeSwitchRepository(IReadOnlyCollection<string> profiles) => _profiles = profiles;
        public bool Started { get; private set; }
        public Task<IReadOnlyCollection<string>> GetUserProfileCodesAsync(long usuarioId, CancellationToken cancellationToken) => Task.FromResult(_profiles);
        public Task<long> StartSwitchAsync(TenantContextSwitchRequest request, CancellationToken cancellationToken) { Started = true; return Task.FromResult(99L); }
        public Task FinishSwitchAsync(long logId, long usuarioGlobalId, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task<IReadOnlyCollection<TenantContextLogItem>> GetLogsAsync(long? usuarioGlobalId, long? tenantId, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyCollection<TenantContextLogItem>>(Array.Empty<TenantContextLogItem>());
    }
}
