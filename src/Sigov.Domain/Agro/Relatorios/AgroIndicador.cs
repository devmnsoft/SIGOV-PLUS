using Sigov.Domain.Common;

namespace Sigov.Domain.Agro.Relatorios;

public sealed class AgroIndicador : AggregateRoot
{
    public AgroIndicador(long tenantId, long? entidadeId, string codigo, string nome, AgroIndicadorCategoria categoria, bool publico = false, bool contemDadoPessoal = false)
    {
        if (tenantId <= 0) throw new ArgumentException("Tenant é obrigatório.", nameof(tenantId));
        if (publico && contemDadoPessoal) throw new ArgumentException("Indicador público não pode expor dado pessoal.", nameof(publico));
        TenantId = tenantId;
        EntidadeId = entidadeId;
        Codigo = Required(codigo, "Indicador exige código.");
        Nome = Required(nome, "Indicador exige nome.");
        Categoria = categoria;
        Publico = publico;
        ContemDadoPessoal = contemDadoPessoal;
    }
    public long TenantId { get; }
    public long? EntidadeId { get; }
    public string Codigo { get; }
    public string Nome { get; }
    public AgroIndicadorCategoria Categoria { get; }
    public bool Publico { get; }
    public bool ContemDadoPessoal { get; }
    private static string Required(string value, string message) => string.IsNullOrWhiteSpace(value) ? throw new ArgumentException(message) : value.Trim();
}
