using Sigov.Application.Common;
using Sigov.Domain.Common;

namespace Sigov.Application.Rh;

public static class RhPermissoes
{
    public const string Modulo = "rh";
    public const string Visualizar = "rh.registros.visualizar";
    public const string Criar = "rh.registros.criar";
    public const string Editar = "rh.registros.editar";
    public const string Excluir = "rh.registros.excluir";
    public const string Exportar = "rh.exportar";
    public const string Portal = "rh.portal.visualizar";
    public const string Dashboard = "rh.dashboard.visualizar";
    public const string IntegrarFinanceiro = "rh.financeiro.integrar";
}

public sealed record RhFiltro(int Page = 1, int PageSize = 20, string? Termo = null, bool? Ativo = null);
public sealed record RhRegistroCreateRequest(Dictionary<string, object?> Dados);
public sealed record RhRegistroUpdateRequest(Dictionary<string, object?> Dados, bool Ativo = true);
public sealed record RhRegistroResponse(long Id, string Recurso, Dictionary<string, object?> Dados, bool Ativo, DateTimeOffset CreatedAt, DateTimeOffset? UpdatedAt);
public sealed record RhDashboardResponse(long ServidoresAtivos, long VinculosAtivos, long FolhasAbertas, long FeriasProgramadas, long AfastamentosAtivos, decimal TotalFolhaMes);
public sealed record RhFinanceiroIntegracaoRequest(long FolhaId, DateOnly DataCompetencia, long? NaturezaDespesaId, long? FonteRecursoId, string Historico);
public sealed record RhPortalResumoResponse(long ServidorId, string Nome, IReadOnlyCollection<RhRegistroResponse> Contracheques, IReadOnlyCollection<RhRegistroResponse> Ferias, IReadOnlyCollection<RhRegistroResponse> Afastamentos);

public interface IRhRepository
{
    Task<PagedResult<RhRegistroResponse>> ListarAsync(long tenantId, string recurso, RhFiltro filtro, CancellationToken ct);
    Task<RhRegistroResponse?> ObterAsync(long tenantId, string recurso, long id, CancellationToken ct);
    Task<long> CriarAsync(long tenantId, string recurso, RhRegistroCreateRequest request, long? usuarioId, CancellationToken ct);
    Task AtualizarAsync(long tenantId, string recurso, long id, RhRegistroUpdateRequest request, long? usuarioId, CancellationToken ct);
    Task ExcluirAsync(long tenantId, string recurso, long id, long? usuarioId, CancellationToken ct);
    Task<RhDashboardResponse> DashboardAsync(long tenantId, CancellationToken ct);
    Task<RhPortalResumoResponse?> PortalServidorAsync(long tenantId, long servidorId, CancellationToken ct);
    Task<long> PrepararIntegracaoFinanceiraAsync(long tenantId, RhFinanceiroIntegracaoRequest request, long? usuarioId, CancellationToken ct);
    Task<byte[]> ExportarAsync(long tenantId, string recurso, string formato, CancellationToken ct);
    Task<bool> ExercicioAbertoAsync(long tenantId, long? exercicioId, CancellationToken ct);
}

public interface IRhService
{
    Task<Result<PagedResult<RhRegistroResponse>>> ListarAsync(string recurso, RhFiltro filtro, CancellationToken ct);
    Task<Result<RhRegistroResponse>> ObterAsync(string recurso, long id, CancellationToken ct);
    Task<Result<long>> CriarAsync(string recurso, RhRegistroCreateRequest request, CancellationToken ct);
    Task<Result> AtualizarAsync(string recurso, long id, RhRegistroUpdateRequest request, CancellationToken ct);
    Task<Result> ExcluirAsync(string recurso, long id, CancellationToken ct);
    Task<Result<RhDashboardResponse>> DashboardAsync(CancellationToken ct);
    Task<Result<RhPortalResumoResponse>> PortalServidorAsync(long servidorId, CancellationToken ct);
    Task<Result<long>> IntegrarFinanceiroAsync(RhFinanceiroIntegracaoRequest request, CancellationToken ct);
    Task<Result<byte[]>> ExportarAsync(string recurso, string formato, CancellationToken ct);
}
