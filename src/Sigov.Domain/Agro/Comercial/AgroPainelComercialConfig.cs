using Sigov.Domain.Common;

namespace Sigov.Domain.Agro.Comercial;

public sealed class AgroPainelComercialConfig : AggregateRoot
{
    public AgroPainelComercialConfig(long tenantId, long? entidadeId, string titulo, string? subtitulo = null)
    {
        if (tenantId <= 0) throw new ArgumentException("Painel comercial respeita tenant obrigatório.", nameof(tenantId));
        TenantId = tenantId; EntidadeId = entidadeId; Titulo = string.IsNullOrWhiteSpace(titulo) ? throw new ArgumentException("Título é obrigatório.", nameof(titulo)) : titulo.Trim(); Subtitulo = subtitulo;
    }
    public long TenantId { get; } public long? EntidadeId { get; } public string Titulo { get; } public string? Subtitulo { get; }
}
