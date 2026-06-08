namespace Sigov.Application.Agro.Relatorios;

public sealed class AgroExportService
{
    private readonly IAgroExportService _inner;
    public AgroExportService(IAgroExportService inner) => _inner = inner;
    public Task<Sigov.Domain.Common.Result<AgroExportResponse>> ExportarAsync(AgroExportRequest request, CancellationToken cancellationToken) => _inner.ExportarAsync(request, cancellationToken);
}
