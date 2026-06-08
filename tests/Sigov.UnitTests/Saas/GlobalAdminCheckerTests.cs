using FluentAssertions;
using Sigov.Application.Saas.Context;
using Xunit;

namespace Sigov.UnitTests.Saas;

public sealed class GlobalAdminCheckerTests
{
    [Fact]
    public async Task Reconhece_administrador_geral()
    {
        var checker = new GlobalAdminChecker(new FakeSwitchRepository(new[] { "ADMINISTRADOR_GERAL" }));
        var result = await checker.IsGlobalAdminAsync(1, CancellationToken.None);
        result.Should().BeTrue();
    }

    private sealed class FakeSwitchRepository : ITenantContextSwitchRepository
    {
        private readonly IReadOnlyCollection<string> _profiles;
        public FakeSwitchRepository(IReadOnlyCollection<string> profiles) => _profiles = profiles;
        public Task<IReadOnlyCollection<string>> GetUserProfileCodesAsync(long usuarioId, CancellationToken cancellationToken) => Task.FromResult(_profiles);
        public Task<long> StartSwitchAsync(TenantContextSwitchRequest request, CancellationToken cancellationToken) => Task.FromResult(1L);
        public Task FinishSwitchAsync(long logId, long usuarioGlobalId, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task<IReadOnlyCollection<TenantContextLogItem>> GetLogsAsync(long? usuarioGlobalId, long? tenantId, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyCollection<TenantContextLogItem>>(Array.Empty<TenantContextLogItem>());
    }
}
