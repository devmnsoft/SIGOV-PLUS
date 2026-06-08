using Sigov.Domain.Common;

namespace Sigov.Domain.Agro;

public sealed class Cultura : AggregateRoot
{
    public Cultura(long tenantId, long entidadeId, string codigo, string nome, string tipoCultura, int? cicloDias, string unidadeMedida)
    {
        if (tenantId <= 0) throw new ArgumentException("Tenant é obrigatório.", nameof(tenantId));
        if (entidadeId <= 0) throw new ArgumentException("Entidade é obrigatória.", nameof(entidadeId));
        if (string.IsNullOrWhiteSpace(codigo)) throw new ArgumentException("Cultura exige código.", nameof(codigo));
        if (string.IsNullOrWhiteSpace(nome)) throw new ArgumentException("Cultura exige nome.", nameof(nome));
        if (string.IsNullOrWhiteSpace(unidadeMedida)) throw new ArgumentException("Unidade de medida é obrigatória.", nameof(unidadeMedida));
        if (cicloDias < 0) throw new ArgumentException("Ciclo em dias não pode ser negativo.", nameof(cicloDias));
        TenantId = tenantId; EntidadeId = entidadeId; Codigo = codigo.Trim(); Nome = nome.Trim(); TipoCultura = string.IsNullOrWhiteSpace(tipoCultura) ? "OUTRO" : tipoCultura.Trim(); CicloDias = cicloDias; UnidadeMedida = unidadeMedida.Trim();
    }
    public long TenantId { get; }
    public long EntidadeId { get; }
    public string Codigo { get; }
    public string Nome { get; }
    public string TipoCultura { get; }
    public int? CicloDias { get; }
    public string UnidadeMedida { get; }
}
