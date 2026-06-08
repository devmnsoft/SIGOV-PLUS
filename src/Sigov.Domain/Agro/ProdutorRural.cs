using Sigov.Domain.Common;

namespace Sigov.Domain.Agro;

public sealed class ProdutorRural : AggregateRoot
{
    public ProdutorRural(long tenantId, long entidadeId, long pessoaId, string codigoProdutor, string tipoProdutor, string situacao)
    {
        if (tenantId <= 0) throw new ArgumentException("Tenant é obrigatório.", nameof(tenantId));
        if (entidadeId <= 0) throw new ArgumentException("Entidade é obrigatória.", nameof(entidadeId));
        if (pessoaId <= 0) throw new ArgumentException("Produtor exige pessoa.", nameof(pessoaId));
        TenantId = tenantId; EntidadeId = entidadeId; PessoaId = pessoaId;
        CodigoProdutor = Required(codigoProdutor, "Código do produtor é obrigatório.");
        TipoProdutor = Required(tipoProdutor, "Tipo do produtor é obrigatório.");
        Situacao = Required(situacao, "Situação do produtor é obrigatória.");
        DataCadastro = DateOnly.FromDateTime(DateTime.UtcNow);
    }
    public long TenantId { get; }
    public long EntidadeId { get; }
    public long PessoaId { get; }
    public string CodigoProdutor { get; }
    public string TipoProdutor { get; }
    public string Situacao { get; }
    public DateOnly DataCadastro { get; }
    private static string Required(string value, string message) => string.IsNullOrWhiteSpace(value) ? throw new ArgumentException(message) : value.Trim();
}
