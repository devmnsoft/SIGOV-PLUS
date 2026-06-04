using SIGOV.Domain.Common;

namespace SIGOV.Domain.Tributario;

public sealed class Contribuinte : AggregateRoot
{
    public Contribuinte(string nome)
    {
        Nome = string.IsNullOrWhiteSpace(nome) ? throw new ArgumentException("Nome é obrigatório.", nameof(nome)) : nome.Trim();
    }

    public string Nome { get; private set; }
}
