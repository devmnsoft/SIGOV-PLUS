using Sigov.Domain.Common;

namespace Sigov.Application.Agro.Relatorios;

public sealed class AgroRelatorioExportService
{
    private readonly IAgroExportService _exportService;
    public AgroRelatorioExportService(IAgroExportService exportService) => _exportService = exportService;
    public Task<Result<AgroExportResponse>> ExportarAsync(AgroExportRequest request, CancellationToken cancellationToken) => _exportService.ExportarAsync(request, cancellationToken);
}
