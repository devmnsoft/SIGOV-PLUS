using SIGOV.Domain.Common;

namespace SIGOV.Domain.Saude;

public sealed class Paciente : AggregateRoot
{
    public Paciente(string nome)
    {
        Nome = string.IsNullOrWhiteSpace(nome) ? throw new ArgumentException("Nome é obrigatório.", nameof(nome)) : nome.Trim();
    }

    public string Nome { get; private set; }
}
