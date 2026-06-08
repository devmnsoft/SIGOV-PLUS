using Sigov.Domain.Agro.Enums;
using Sigov.Domain.Common;

namespace Sigov.Domain.Agro;

public sealed class Insumo : AggregateRoot
{
    public Insumo(long tenantId, long entidadeId, string codigo, string nome, AgroInsumoTipo tipoInsumo, string unidadeMedida)
    {
        if (tenantId <= 0) throw new ArgumentException("Tenant é obrigatório.", nameof(tenantId));
        if (entidadeId <= 0) throw new ArgumentException("Entidade é obrigatória.", nameof(entidadeId));
        TenantId = tenantId; EntidadeId = entidadeId; Codigo = Required(codigo, "Insumo exige código."); Nome = Required(nome, "Insumo exige nome."); TipoInsumo = tipoInsumo; UnidadeMedida = Required(unidadeMedida, "Insumo exige unidade de medida.");
    }
    public long TenantId { get; }
    public long EntidadeId { get; }
    public string Codigo { get; }
    public string Nome { get; }
    public AgroInsumoTipo TipoInsumo { get; }
    public string UnidadeMedida { get; }
    private static string Required(string value, string message) => string.IsNullOrWhiteSpace(value) ? throw new ArgumentException(message) : value.Trim();
}
