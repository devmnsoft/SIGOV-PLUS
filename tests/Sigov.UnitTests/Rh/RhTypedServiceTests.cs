using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Sigov.Application.Common;
using Sigov.Application.Rh;
using Sigov.Application.Rh.Dto;
using Sigov.Domain.Common;
using Xunit;

namespace Sigov.UnitTests.Rh;

public sealed class RhTypedServiceTests
{
    [Fact]
    public async Task Service_Tipado_Valida_Cpf_Invalido_Antes_Do_Service_Generico()
    {
        var inner = new FakeRhService();
        var service = new RhTypedService(inner, NullLogger<RhTypedService>.Instance);
        var result = await service.CriarServidorAsync(new ServidorCreateRequest("MAT", "Maria", "123"), CancellationToken.None);
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("CPF");
        inner.CriarChamadas.Should().Be(0);
    }

    [Fact]
    public async Task Service_Tipado_Valida_Folha_Mes_Entre_1_E_13()
    {
        var service = new RhTypedService(new FakeRhService(), NullLogger<RhTypedService>.Instance);
        var result = await service.CriarFolhaAsync(new FolhaCreateRequest(2026, 14, "mensal"), CancellationToken.None);
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Mês da folha");
    }

    [Fact]
    public async Task Service_Tipado_Nao_Aceita_Lancamento_Negativo()
    {
        var service = new RhTypedService(new FakeRhService(), NullLogger<RhTypedService>.Instance);
        var result = await service.CriarLancamentoFolhaAsync(new FolhaLancamentoCreateRequest(1, 1, 1, -1m), CancellationToken.None);
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("negativo");
    }

    [Fact]
    public async Task Service_Tipado_Valida_Periodos_De_Ferias_E_Afastamento()
    {
        var service = new RhTypedService(new FakeRhService(), NullLogger<RhTypedService>.Instance);
        var ferias = await service.ProgramarFeriasAsync(new FeriasCreateRequest(1, new DateOnly(2026, 2, 10), new DateOnly(2026, 2, 1)), CancellationToken.None);
        var afastamento = await service.RegistrarAfastamentoAsync(new AfastamentoCreateRequest(1, new DateOnly(2026, 2, 10), new DateOnly(2026, 2, 1), "Licença"), CancellationToken.None);
        ferias.IsFailure.Should().BeTrue();
        afastamento.IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task Service_Tipado_Valida_Historico_Antes_Da_Integracao_Financeira()
    {
        var inner = new FakeRhService();
        var service = new RhTypedService(inner, NullLogger<RhTypedService>.Instance);

        var result = await service.IntegrarFinanceiroAsync(new RhFinanceiroIntegracaoRequest(1, new DateOnly(2026, 1, 1), null, null, " "), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Histórico");
        inner.IntegrarChamadas.Should().Be(0);
    }

    private sealed class FakeRhService : IRhService
    {
        public int CriarChamadas { get; private set; }
        public int IntegrarChamadas { get; private set; }
        public Task<Result<PagedResult<RhRegistroResponse>>> ListarAsync(string recurso, RhFiltro filtro, CancellationToken ct) => Task.FromResult(Result<PagedResult<RhRegistroResponse>>.Success(PagedResult<RhRegistroResponse>.Empty(filtro.Page, filtro.PageSize)));
        public Task<Result<RhRegistroResponse>> ObterAsync(string recurso, long id, CancellationToken ct) => Task.FromResult(Result<RhRegistroResponse>.Failure("not found"));
        public Task<Result<long>> CriarAsync(string recurso, RhRegistroCreateRequest request, CancellationToken ct) { CriarChamadas++; return Task.FromResult(Result<long>.Success(1)); }
        public Task<Result> AtualizarAsync(string recurso, long id, RhRegistroUpdateRequest request, CancellationToken ct) => Task.FromResult(Result.Success());
        public Task<Result> ExcluirAsync(string recurso, long id, CancellationToken ct) => Task.FromResult(Result.Success());
        public Task<Result<RhDashboardResponse>> DashboardAsync(CancellationToken ct) => Task.FromResult(Result<RhDashboardResponse>.Success(new RhDashboardResponse(0, 0, 0, 0, 0, 0m)));
        public Task<Result<RhPortalResumoResponse>> PortalServidorAsync(long servidorId, CancellationToken ct) => Task.FromResult(Result<RhPortalResumoResponse>.Failure("not found"));
        public Task<Result<long>> IntegrarFinanceiroAsync(RhFinanceiroIntegracaoRequest request, CancellationToken ct)
        {
            IntegrarChamadas++;
            return Task.FromResult(Result<long>.Success(99));
        }
        public Task<Result<byte[]>> ExportarAsync(string recurso, string formato, CancellationToken ct) => Task.FromResult(Result<byte[]>.Success(Array.Empty<byte>()));
    }
}
