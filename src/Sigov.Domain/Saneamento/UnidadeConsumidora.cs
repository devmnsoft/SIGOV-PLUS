using Sigov.Domain.Common;

namespace Sigov.Domain.Saneamento;

public sealed class UnidadeConsumidora : AggregateRoot
{
    public UnidadeConsumidora(string nome)
    {
        Nome = string.IsNullOrWhiteSpace(nome) ? throw new ArgumentException("Nome é obrigatório.", nameof(nome)) : nome.Trim();
    }

    public string Nome { get; private set; }
}
