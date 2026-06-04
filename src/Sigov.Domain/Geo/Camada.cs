using Sigov.Domain.Common;

namespace Sigov.Domain.Geo;

public sealed class Camada : AggregateRoot
{
    public Camada(string nome)
    {
        Nome = string.IsNullOrWhiteSpace(nome) ? throw new ArgumentException("Nome é obrigatório.", nameof(nome)) : nome.Trim();
    }

    public string Nome { get; private set; }
}
