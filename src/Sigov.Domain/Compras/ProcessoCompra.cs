using Sigov.Domain.Common;

namespace Sigov.Domain.Compras;

public sealed class ProcessoCompra : AggregateRoot
{
    public ProcessoCompra(string nome)
    {
        Nome = string.IsNullOrWhiteSpace(nome) ? throw new ArgumentException("Nome é obrigatório.", nameof(nome)) : nome.Trim();
    }

    public string Nome { get; private set; }
}
