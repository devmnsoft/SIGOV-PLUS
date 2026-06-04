using SIGOV.Domain.Common;

namespace SIGOV.Domain.Rh;

public sealed class Servidor : AggregateRoot
{
    public Servidor(string nome)
    {
        Nome = string.IsNullOrWhiteSpace(nome) ? throw new ArgumentException("Nome é obrigatório.", nameof(nome)) : nome.Trim();
    }

    public string Nome { get; private set; }
}
