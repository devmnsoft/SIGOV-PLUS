using Sigov.Domain.Common;

namespace Sigov.Domain.Audit;

public sealed class TrilhaAuditoria : AggregateRoot
{
    public TrilhaAuditoria(string nome)
    {
        Nome = string.IsNullOrWhiteSpace(nome) ? throw new ArgumentException("Nome é obrigatório.", nameof(nome)) : nome.Trim();
    }

    public string Nome { get; private set; }
}
