using Microsoft.Extensions.Configuration;
using Sigov.Application.Abstractions;

namespace Sigov.Infrastructure.Common;

/// <summary>
/// Ciclo 2 Sprint 2 (BR-2.5): relógio central configurável. O instante UTC permanece como hoje;
/// a data operacional usa a data LOCAL do host (o dia que o operador vê) — e pode ser fixada via
/// ambiente SIGOV_CLOCK_DATAFIXA (yyyy-MM-dd) quando a instância for de desenvolvimento/homologação.
/// </summary>
public sealed class DateTimeProvider : IDateTimeProvider
{
    private readonly DateOnly? _dataFixa;

    public DateTimeProvider(IConfiguration? config = null)
    {
        var valor = config?["SIGOV_CLOCK_DATAFIXA"];
        _dataFixa = string.IsNullOrWhiteSpace(valor) ? null : (DateOnly.TryParse(valor, out var d) ? d : null);
    }

    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;

    public DateOnly DataAtual => _dataFixa ?? DateOnly.FromDateTime(DateTime.Today);
}
