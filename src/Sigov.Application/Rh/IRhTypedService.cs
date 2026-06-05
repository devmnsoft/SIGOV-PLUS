using Sigov.Application.Common;
using Sigov.Application.Rh.Dto;
using Sigov.Domain.Common;

namespace Sigov.Application.Rh;

public interface IRhTypedService
{
    Task<Result<long>> CriarServidorAsync(ServidorCreateRequest request, CancellationToken ct);
    Task<Result> AtualizarServidorAsync(long id, ServidorUpdateRequest request, CancellationToken ct);
    Task<Result<ServidorResponse>> ObterServidorAsync(long id, CancellationToken ct);
    Task<Result<PagedResult<ServidorResponse>>> ListarServidoresAsync(ServidorFiltro filtro, CancellationToken ct);
    Task<Result> ExcluirServidorAsync(long id, CancellationToken ct);
    Task<Result<long>> CriarCargoAsync(CargoCreateRequest request, CancellationToken ct);
    Task<Result<PagedResult<CargoResponse>>> ListarCargosAsync(CargoFiltro filtro, CancellationToken ct);
    Task<Result<long>> CriarLotacaoAsync(LotacaoCreateRequest request, CancellationToken ct);
    Task<Result<PagedResult<LotacaoResponse>>> ListarLotacoesAsync(LotacaoFiltro filtro, CancellationToken ct);
    Task<Result<long>> CriarVinculoAsync(VinculoCreateRequest request, CancellationToken ct);
    Task<Result<PagedResult<VinculoResponse>>> ListarVinculosAsync(VinculoFiltro filtro, CancellationToken ct);
    Task<Result<long>> CriarFolhaAsync(FolhaCreateRequest request, CancellationToken ct);
    Task<Result<PagedResult<FolhaResponse>>> ListarFolhasAsync(FolhaFiltro filtro, CancellationToken ct);
    Task<Result> FecharFolhaAsync(long id, CancellationToken ct);
    Task<Result<long>> CriarEventoFolhaAsync(FolhaEventoCreateRequest request, CancellationToken ct);
    Task<Result<long>> CriarLancamentoFolhaAsync(FolhaLancamentoCreateRequest request, CancellationToken ct);
    Task<Result<long>> RegistrarPontoAsync(PontoCreateRequest request, CancellationToken ct);
    Task<Result<PagedResult<PontoResponse>>> ListarPontosAsync(PontoFiltro filtro, CancellationToken ct);
    Task<Result<long>> ProgramarFeriasAsync(FeriasCreateRequest request, CancellationToken ct);
    Task<Result<long>> RegistrarAfastamentoAsync(AfastamentoCreateRequest request, CancellationToken ct);
    Task<Result<long>> RegistrarSaudeOcupacionalAsync(SaudeOcupacionalCreateRequest request, CancellationToken ct);
    Task<Result<long>> CriarEventoEsocialAsync(EsocialEventoCreateRequest request, CancellationToken ct);
    Task<Result<long>> IntegrarFinanceiroAsync(RhFinanceiroIntegracaoRequest request, CancellationToken ct);
    Task<Result<PortalServidorResponse>> ObterPortalServidorAsync(long servidorId, CancellationToken ct);
}
