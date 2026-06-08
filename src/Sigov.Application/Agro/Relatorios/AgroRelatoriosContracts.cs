using Sigov.Domain.Common;

namespace Sigov.Application.Agro.Relatorios;

public sealed record AgroIndicadorCreateRequest(string Codigo, string Nome, string Categoria, string? Descricao = null, string? UnidadeMedida = null, bool Publico = false, bool ContemDadoPessoal = false, long? EntidadeId = null);
public sealed record AgroIndicadorResponse(long Id, long TenantId, long? EntidadeId, string Codigo, string Nome, string Categoria, string? Descricao, string? UnidadeMedida, bool Publico, bool Ativo);
public sealed record AgroIndicadorValorResponse(long Id, long TenantId, long? EntidadeId, long? ExercicioId, long IndicadorId, string? Competencia, decimal Valor, DateTime CalculadoAt);
public sealed record AgroRelatorioModeloCreateRequest(string Codigo, string Nome, string TipoRelatorio, string? Descricao = null, string FormatoPadrao = "HTML", bool PublicoNoTenant = false, bool ContemDadosPessoais = false, bool ContemDadosSensiveis = false, long? EntidadeId = null);
public sealed record AgroRelatorioModeloResponse(long Id, long TenantId, long? EntidadeId, string Codigo, string Nome, string TipoRelatorio, string FormatoPadrao, bool PublicoNoTenant, bool ContemDadosPessoais, bool Ativo);
public sealed record ExecutarAgroRelatorioRequest(string Formato = "HTML", long? EntidadeId = null, long? ExercicioId = null, string? ParametrosJson = null);
public sealed record AgroRelatorioExecucaoResponse(long Id, long TenantId, long? EntidadeId, long? ExercicioId, long? ModeloId, long? UsuarioId, string Formato, string Status, long? TotalLinhas, DateTime IniciouAt, DateTime? FinalizouAt, string? Erro);
public sealed record AgroExportRequest(string Dataset, string Formato, long? EntidadeId = null, long? ExercicioId = null, bool MascararDadosPessoais = true);
public sealed record AgroExportResponse(string FileName, string ContentType, string Content, long TotalLinhas, bool Mascarado, bool Anonimizado);

public interface IAgroIndicadorRepository
{
    Task<IReadOnlyCollection<AgroIndicadorResponse>> ListarIndicadoresAsync(long tenantId, long? entidadeId, int page, int pageSize, CancellationToken cancellationToken);
    Task<AgroIndicadorResponse> CriarIndicadorAsync(long tenantId, long? entidadeId, long usuarioId, AgroIndicadorCreateRequest request, CancellationToken cancellationToken);
    Task<IReadOnlyCollection<AgroIndicadorValorResponse>> ListarValoresAsync(long tenantId, long? entidadeId, long indicadorId, int page, int pageSize, CancellationToken cancellationToken);
}
public interface IAgroRelatoriosRepository
{
    Task<IReadOnlyCollection<AgroRelatorioModeloResponse>> ListarModelosAsync(long tenantId, long? entidadeId, int page, int pageSize, CancellationToken cancellationToken);
    Task<AgroRelatorioModeloResponse> CriarModeloAsync(long tenantId, long? entidadeId, long usuarioId, AgroRelatorioModeloCreateRequest request, CancellationToken cancellationToken);
    Task<AgroRelatorioExecucaoResponse> ExecutarAsync(long tenantId, long? entidadeId, long? exercicioId, long usuarioId, long modeloId, ExecutarAgroRelatorioRequest request, CancellationToken cancellationToken);
    Task<IReadOnlyCollection<AgroRelatorioExecucaoResponse>> ListarExecucoesAsync(long tenantId, long? entidadeId, int page, int pageSize, CancellationToken cancellationToken);
    Task<AgroRelatorioExecucaoResponse?> ObterExecucaoAsync(long tenantId, long id, CancellationToken cancellationToken);
    Task<AgroExportResponse> ExportarAsync(long tenantId, long? entidadeId, long? exercicioId, long usuarioId, AgroExportRequest request, CancellationToken cancellationToken);
}
public interface IAgroIndicadorService
{
    Task<Result<IReadOnlyCollection<AgroIndicadorResponse>>> ListarAsync(int page, int pageSize, CancellationToken cancellationToken);
    Task<Result<AgroIndicadorResponse>> CriarAsync(AgroIndicadorCreateRequest request, CancellationToken cancellationToken);
    Task<Result<IReadOnlyCollection<AgroIndicadorValorResponse>>> ListarValoresAsync(long id, int page, int pageSize, CancellationToken cancellationToken);
}
public interface IAgroRelatorioService
{
    Task<Result<IReadOnlyCollection<AgroRelatorioModeloResponse>>> ListarModelosAsync(int page, int pageSize, CancellationToken cancellationToken);
    Task<Result<AgroRelatorioModeloResponse>> CriarModeloAsync(AgroRelatorioModeloCreateRequest request, CancellationToken cancellationToken);
    Task<Result<AgroRelatorioExecucaoResponse>> ExecutarAsync(long modeloId, ExecutarAgroRelatorioRequest request, CancellationToken cancellationToken);
    Task<Result<IReadOnlyCollection<AgroRelatorioExecucaoResponse>>> ListarExecucoesAsync(int page, int pageSize, CancellationToken cancellationToken);
    Task<Result<AgroRelatorioExecucaoResponse>> ObterExecucaoAsync(long id, CancellationToken cancellationToken);
}
public interface IAgroExportService { Task<Result<AgroExportResponse>> ExportarAsync(AgroExportRequest request, CancellationToken cancellationToken); }
