using Sigov.Domain.Common;

namespace Sigov.Domain.Core;

public sealed class Entidade : AggregateRoot
{
    public Entidade(string nome)
    {
        Nome = string.IsNullOrWhiteSpace(nome) ? throw new ArgumentException("Nome é obrigatório.", nameof(nome)) : nome.Trim();
    }

    public string Nome { get; private set; }
}
