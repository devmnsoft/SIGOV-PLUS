using Sigov.Domain.Common;

namespace Sigov.Domain.Educacao;

public sealed class Escola : AggregateRoot
{
    public Escola(string nome)
    {
        Nome = string.IsNullOrWhiteSpace(nome) ? throw new ArgumentException("Nome é obrigatório.", nameof(nome)) : nome.Trim();
    }

    public string Nome { get; private set; }
}
