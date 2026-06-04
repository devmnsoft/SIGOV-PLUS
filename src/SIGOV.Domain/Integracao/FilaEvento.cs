using SIGOV.Domain.Common;

namespace SIGOV.Domain.Integracao;

public sealed class FilaEvento : AggregateRoot
{
    public FilaEvento(string nome)
    {
        Nome = string.IsNullOrWhiteSpace(nome) ? throw new ArgumentException("Nome é obrigatório.", nameof(nome)) : nome.Trim();
    }

    public string Nome { get; private set; }
}
