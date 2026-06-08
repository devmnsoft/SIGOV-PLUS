using Sigov.Domain.Common;

namespace Sigov.Domain.Agro;

public sealed class ImplementoRural : AggregateRoot
{
    public ImplementoRural(long tenantId, long entidadeId, string codigo, string nome, string tipoImplemento, string situacao)
    {
        if (tenantId <= 0) throw new ArgumentException("Tenant é obrigatório.", nameof(tenantId));
        if (entidadeId <= 0) throw new ArgumentException("Entidade é obrigatória.", nameof(entidadeId));
        TenantId = tenantId; EntidadeId = entidadeId; Codigo = Required(codigo, "Implemento exige código."); Nome = Required(nome, "Implemento exige nome."); TipoImplemento = Required(tipoImplemento, "Tipo do implemento é obrigatório."); Situacao = Required(situacao, "Situação do implemento é obrigatória.");
    }
    public long TenantId { get; }
    public long EntidadeId { get; }
    public string Codigo { get; }
    public string Nome { get; }
    public string TipoImplemento { get; }
    public string Situacao { get; }
    private static string Required(string value, string message) => string.IsNullOrWhiteSpace(value) ? throw new ArgumentException(message) : value.Trim();
}
