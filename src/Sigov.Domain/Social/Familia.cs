using Sigov.Domain.Common;

namespace Sigov.Domain.Social;

public sealed class Familia : AggregateRoot
{
    public Familia(string nome)
    {
        Nome = string.IsNullOrWhiteSpace(nome) ? throw new ArgumentException("Nome é obrigatório.", nameof(nome)) : nome.Trim();
    }

    public string Nome { get; private set; }
}
