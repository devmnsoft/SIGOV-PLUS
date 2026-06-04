using SIGOV.Domain.Common;

namespace SIGOV.Domain.Compras;

public sealed class ProcessoCompra : AggregateRoot
{
    public ProcessoCompra(string nome)
    {
        Nome = string.IsNullOrWhiteSpace(nome) ? throw new ArgumentException("Nome é obrigatório.", nameof(nome)) : nome.Trim();
    }

    public string Nome { get; private set; }
}
