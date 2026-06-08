using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Sigov.Application.Abstractions;
using Sigov.Application.Agro.Dashboard;
using Sigov.Application.Saas;
using Xunit;

namespace Sigov.UnitTests.Agro;

public sealed class AgroDashboardTests
{
    [Fact]
    public async Task Dashboard_Deve_Retornar_Indicadores_Quando_Contexto_Valido()
    {
        var service = new AgroDashboardService(new TenantFake(10, 20), new UserFake(30, true), new ModuloFake(true), new PermissionFake(true), new DashboardRepoFake(), NullLogger<AgroDashboardService>.Instance);

        var result = await service.ObterAsync(CancellationToken.None).ConfigureAwait(false);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.TotalCamadas.Should().Be(2);
        result.Value.TotalFeicoes.Should().Be(3);
    }

    private sealed record TenantFake(long? TenantId, long? EntidadeId) : ICurrentTenant
    {
        public string? TenantSlug => "tenant-dev";
        public long? ExercicioId => 2026;
    }

    private sealed record UserFake(long? UsuarioId, bool IsAuthenticated) : ICurrentUser
    {
        public string? Nome => "Usuário Agro";
    }

    private sealed record ModuloFake(bool Enabled) : IModuloLicenciamentoService
    {
        public Task<bool> IsModuleEnabledAsync(long tenantId, string moduleCode, CancellationToken cancellationToken) => Task.FromResult(Enabled && moduleCode == "agro");
    }

    private sealed record PermissionFake(bool Allowed) : IPermissionService
    {
        public Task<bool> HasPermissionAsync(long usuarioId, string modulo, string recurso, string acao, CancellationToken cancellationToken = default) => Task.FromResult(Allowed && modulo == "agro" && recurso == "dashboard" && acao == "visualizar");
    }

    private sealed class DashboardRepoFake : IAgroDashboardRepository
    {
        public Task<AgroDashboardResponse> ObterAsync(long tenantId, long? entidadeId, CancellationToken cancellationToken) => Task.FromResult(new AgroDashboardResponse(tenantId, entidadeId, 2, 3, 1, 0, 0, 0, 0, 0));
    }
}
