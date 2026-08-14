using Sigov.Application.Abstractions;

namespace Sigov.Application.Relatorios;

public sealed record RelatorioExecutivoFiltro(DateOnly? Inicio = null, DateOnly? Fim = null, long? EntidadeId = null, long? ExercicioId = null, string? Modulo = null);
public sealed record RelatorioExecutivoFinanceiroDto(decimal Previsto, decimal Atualizado, decimal Empenhado, decimal Liquidado, decimal Pago, decimal Saldo, long IntegracoesPendentes);
public sealed record RelatorioExecutivoTributarioDto(decimal Lancado, decimal Arrecadado, decimal EmAberto, decimal Parcelado, decimal DividaAtiva, decimal Baixado);
public sealed record RelatorioExecutivoRhFolhaDto(long ServidoresAtivos, long FolhasAbertas, decimal TotalBruto, decimal TotalLiquido, long Criticas);
public sealed record RelatorioExecutivoEducacaoDto(long Alunos, long Matriculas, decimal FrequenciaMedia, long RegistrosRisco);
public sealed record RelatorioExecutivoWidgetDto(string Modulo, string Titulo, string Valor, string Tendencia, string DrillDownUrl);
public sealed record RelatorioExecutivoDashboardDto(RelatorioExecutivoFinanceiroDto Financeiro, RelatorioExecutivoTributarioDto Tributario, RelatorioExecutivoRhFolhaDto RhFolha, RelatorioExecutivoEducacaoDto Educacao, IReadOnlyCollection<RelatorioExecutivoWidgetDto> Widgets, DateTimeOffset AtualizadoEm, string AvisoLgpd);
public sealed record RelatorioExecutivoExportRequest(string Formato, RelatorioExecutivoFiltro Filtro);
public sealed record RelatorioExecutivoFiltroSalvoRequest(string Nome, string Modulo, RelatorioExecutivoFiltro Filtro);

public interface IRelatorioExecutivoRepository
{
    Task<RelatorioExecutivoDashboardDto> ObterAsync(long tenantId, RelatorioExecutivoFiltro filtro, CancellationToken ct);
    Task<long> SalvarFiltroAsync(long tenantId, long? usuarioId, RelatorioExecutivoFiltroSalvoRequest request, string correlationId, CancellationToken ct);
    Task<IReadOnlyCollection<object>> ListarFiltrosAsync(long tenantId, long? usuarioId, CancellationToken ct);
}
public interface IRelatorioExecutivoService { Task<RelatorioExecutivoDashboardDto> ObterAsync(RelatorioExecutivoFiltro filtro, CancellationToken ct); }
public interface IRelatorioExecutivoWidgetService { Task<IReadOnlyCollection<RelatorioExecutivoWidgetDto>> ListarAsync(RelatorioExecutivoFiltro filtro, CancellationToken ct); }
public interface IRelatorioExecutivoExportService { Task<(byte[] Conteudo, string ContentType, string Nome)> ExportarAsync(string formato, RelatorioExecutivoFiltro filtro, CancellationToken ct); }

public sealed class RelatorioExecutivoService : IRelatorioExecutivoService
{
    private readonly IRelatorioExecutivoRepository _repository; private readonly ICurrentTenant _tenant;
    public RelatorioExecutivoService(IRelatorioExecutivoRepository repository, ICurrentTenant tenant) { _repository = repository; _tenant = tenant; }
    public Task<RelatorioExecutivoDashboardDto> ObterAsync(RelatorioExecutivoFiltro filtro, CancellationToken ct)
    {
        if (!_tenant.TenantId.HasValue) throw new InvalidOperationException("Tenant autenticado é obrigatório.");
        return _repository.ObterAsync(_tenant.TenantId.Value, filtro with { EntidadeId = filtro.EntidadeId ?? _tenant.EntidadeId, ExercicioId = filtro.ExercicioId ?? _tenant.ExercicioId }, ct);
    }
}
public sealed class RelatorioExecutivoWidgetService : IRelatorioExecutivoWidgetService
{
    private readonly IRelatorioExecutivoService _service; public RelatorioExecutivoWidgetService(IRelatorioExecutivoService service) => _service = service;
    public async Task<IReadOnlyCollection<RelatorioExecutivoWidgetDto>> ListarAsync(RelatorioExecutivoFiltro filtro, CancellationToken ct) => (await _service.ObterAsync(filtro, ct).ConfigureAwait(false)).Widgets;
}
public sealed class RelatorioExecutivoExportService : IRelatorioExecutivoExportService
{
    private readonly IRelatorioExecutivoService _service; public RelatorioExecutivoExportService(IRelatorioExecutivoService service) => _service = service;
    public async Task<(byte[] Conteudo, string ContentType, string Nome)> ExportarAsync(string formato, RelatorioExecutivoFiltro filtro, CancellationToken ct)
    {
        var data = await _service.ObterAsync(filtro, ct).ConfigureAwait(false);
        if (formato.Equals("json", StringComparison.OrdinalIgnoreCase)) return (System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(data), "application/json", "relatorio-executivo.json");
        var csv = "modulo;indicador;valor\nfinanceiro;previsto;" + data.Financeiro.Previsto.ToString(System.Globalization.CultureInfo.InvariantCulture) + "\nfinanceiro;pago;" + data.Financeiro.Pago.ToString(System.Globalization.CultureInfo.InvariantCulture) + "\ntributario;lancado;" + data.Tributario.Lancado.ToString(System.Globalization.CultureInfo.InvariantCulture) + "\ntributario;arrecadado;" + data.Tributario.Arrecadado.ToString(System.Globalization.CultureInfo.InvariantCulture) + "\n";
        return (System.Text.Encoding.UTF8.GetBytes(csv), "text/csv", "relatorio-executivo.csv");
    }
}
