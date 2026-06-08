using Sigov.Domain.Common;

namespace Sigov.Domain.Agro.Transparencia;

public sealed class AgroDatasetPublico : AggregateRoot
{
    public AgroDatasetPublico(long tenantId, long? entidadeId, string codigo, string nome, AgroDatasetTipo tipoDataset, bool anonimizado = true, bool publico = false)
    {
        if (tenantId <= 0) throw new ArgumentException("Tenant é obrigatório.", nameof(tenantId));
        if (publico && !anonimizado) throw new ArgumentException("Dataset público deve estar anonimizado.", nameof(publico));
        TenantId = tenantId; EntidadeId = entidadeId; Codigo = Required(codigo, "Dataset exige código."); Nome = Required(nome, "Dataset exige nome."); TipoDataset = tipoDataset; Anonimizado = anonimizado; Publico = publico;
    }
    public long TenantId { get; } public long? EntidadeId { get; } public string Codigo { get; } public string Nome { get; } public AgroDatasetTipo TipoDataset { get; } public bool Anonimizado { get; } public bool Publico { get; }
    public void ValidarPublicacao() { if (!Anonimizado) throw new InvalidOperationException("Dataset não anonimizado não pode ser publicado."); }
    private static string Required(string value, string message) => string.IsNullOrWhiteSpace(value) ? throw new ArgumentException(message) : value.Trim();
}
