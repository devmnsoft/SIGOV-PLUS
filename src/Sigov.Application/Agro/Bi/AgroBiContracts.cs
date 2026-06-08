using Sigov.Domain.Common;

namespace Sigov.Application.Agro.Bi;

public sealed record AgroBiIndicadorCard(string Codigo, string Nome, string Categoria, decimal Valor, string? UnidadeMedida, bool Publico);
public sealed record AgroBiDashboardResponse(long TenantId, long? EntidadeId, IReadOnlyCollection<AgroBiIndicadorCard> Cards, IReadOnlyDictionary<string, decimal> Resumo);
public interface IAgroBiRepository { Task<AgroBiDashboardResponse> ObterDashboardAsync(long tenantId, long? entidadeId, long? exercicioId, CancellationToken cancellationToken); }
public interface IAgroBiService { Task<Result<AgroBiDashboardResponse>> ObterDashboardAsync(CancellationToken cancellationToken); }
