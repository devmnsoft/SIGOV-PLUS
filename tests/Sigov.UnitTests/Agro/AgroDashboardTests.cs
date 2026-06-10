using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Sigov.Application.Agro.Dashboard;
using Sigov.Application.Agro.Permissions;
using Sigov.Domain.Common;
using Xunit;

namespace Sigov.UnitTests.Agro;

public sealed class AgroDashboardTests
{
    [Fact]
    public async Task Dashboard_Deve_Retornar_Indicadores_Quando_Contexto_Valido()
    {
        var access = new AccessCheckerFake(Result<AgroAccessContext>.Success(new AgroAccessContext(10, 20, 2026, 30, new[] { "ADMINISTRADOR_TENANT" }, false)));
        var service = new AgroDashboardService(access, new DashboardRepoFake(), NullLogger<AgroDashboardService>.Instance);

        var result = await service.ObterAsync(CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.TotalCamadas.Should().Be(2);
        result.Value.TotalFeicoes.Should().Be(3);
    }

    private sealed class AccessCheckerFake : IAgroAccessChecker
    {
        private readonly Result<AgroAccessContext> _result;
        public AccessCheckerFake(Result<AgroAccessContext> result) => _result = result;
        public Task<Result<AgroAccessContext>> CheckAsync(AgroAccessRequest request, CancellationToken cancellationToken) => Task.FromResult(_result);
    }

    private sealed class DashboardRepoFake : IAgroDashboardRepository
    {
        public Task<AgroDashboardResponse> ObterAsync(long tenantId, long? entidadeId, CancellationToken cancellationToken) => Task.FromResult(new AgroDashboardResponse(tenantId, entidadeId, 2, 3, 1, 0, 0, 0, 0, 0));
    }
}
