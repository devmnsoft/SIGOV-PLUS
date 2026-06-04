using Sigov.Domain.Common;

namespace Sigov.Domain.Security;

public sealed class Usuario : AggregateRoot
{
    public Usuario(string nome)
    {
        Nome = string.IsNullOrWhiteSpace(nome) ? throw new ArgumentException("Nome é obrigatório.", nameof(nome)) : nome.Trim();
    }

    public string Nome { get; private set; }
}
