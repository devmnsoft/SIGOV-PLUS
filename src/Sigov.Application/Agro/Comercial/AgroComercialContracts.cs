using Sigov.Domain.Common;

namespace Sigov.Application.Agro.Comercial;

public sealed record AgroPainelComercialConfigRequest(string Titulo, string? Subtitulo = null, bool MostrarProdutores = true, bool MostrarProducao = true, bool MostrarPecuaria = true, bool MostrarMapa = true, bool MostrarProgramas = true, bool MostrarEstradas = true, bool MostrarFeiras = true, bool MostrarAgroindustrias = true, long? EntidadeId = null);
public sealed record AgroPainelComercialResponse(long TenantId, long? EntidadeId, string Titulo, string? Subtitulo, IReadOnlyCollection<string> Beneficios, IReadOnlyCollection<string> Funcionalidades, IReadOnlyDictionary<string, decimal> Indicadores, string? CorPrimaria = null, string? LogoUrl = null);
public interface IAgroPainelComercialRepository
{
    Task<AgroPainelComercialResponse> ObterAsync(long tenantId, long? entidadeId, CancellationToken cancellationToken);
    Task<AgroPainelComercialResponse> AtualizarAsync(long tenantId, long? entidadeId, AgroPainelComercialConfigRequest request, CancellationToken cancellationToken);
    Task<AgroPainelComercialResponse?> ObterPublicoAsync(string tenantSlug, CancellationToken cancellationToken);
}
public interface IAgroPainelComercialService
{
    Task<Result<AgroPainelComercialResponse>> ObterAsync(CancellationToken cancellationToken);
    Task<Result<AgroPainelComercialResponse>> AtualizarAsync(AgroPainelComercialConfigRequest request, CancellationToken cancellationToken);
    Task<Result<AgroPainelComercialResponse>> ObterPublicoAsync(string tenantSlug, CancellationToken cancellationToken);
}
