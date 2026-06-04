using Sigov.Domain.Common;

namespace Sigov.Domain.Bi;

public sealed class Dashboard : AggregateRoot
{
    public Dashboard(string nome)
    {
        Nome = string.IsNullOrWhiteSpace(nome) ? throw new ArgumentException("Nome é obrigatório.", nameof(nome)) : nome.Trim();
    }

    public string Nome { get; private set; }
}
