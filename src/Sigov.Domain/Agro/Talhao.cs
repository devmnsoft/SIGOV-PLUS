using Sigov.Domain.Common;

namespace Sigov.Domain.Agro;

public sealed class Talhao : AggregateRoot
{
    public Talhao(long tenantId, long entidadeId, long propriedadeId, string codigo, string nome, decimal areaHa, decimal? latitude, decimal? longitude, string situacao)
    {
        if (tenantId <= 0) throw new ArgumentException("Tenant é obrigatório.", nameof(tenantId));
        if (entidadeId <= 0) throw new ArgumentException("Entidade é obrigatória.", nameof(entidadeId));
        if (propriedadeId <= 0) throw new ArgumentException("Talhão exige propriedade.", nameof(propriedadeId));
        if (string.IsNullOrWhiteSpace(codigo)) throw new ArgumentException("Código do talhão é obrigatório.", nameof(codigo));
        if (string.IsNullOrWhiteSpace(nome)) throw new ArgumentException("Nome do talhão é obrigatório.", nameof(nome));
        if (areaHa <= 0) throw new ArgumentException("Área do talhão deve ser maior que zero.", nameof(areaHa));
        PropriedadeRural.ValidateCoordinates(latitude, longitude);
        if (string.IsNullOrWhiteSpace(situacao)) throw new ArgumentException("Situação do talhão é obrigatória.", nameof(situacao));
        TenantId = tenantId; EntidadeId = entidadeId; PropriedadeId = propriedadeId; Codigo = codigo.Trim(); Nome = nome.Trim(); AreaHa = areaHa; Latitude = latitude; Longitude = longitude; Situacao = situacao.Trim();
    }
    public long TenantId { get; }
    public long EntidadeId { get; }
    public long PropriedadeId { get; }
    public string Codigo { get; }
    public string Nome { get; }
    public decimal AreaHa { get; }
    public decimal? Latitude { get; }
    public decimal? Longitude { get; }
    public string Situacao { get; }
}
