using SIGOV.Domain.Common;

namespace SIGOV.Domain.Lgpd;

public sealed class SolicitacaoTitular : AggregateRoot
{
    public SolicitacaoTitular(string nome)
    {
        Nome = string.IsNullOrWhiteSpace(nome) ? throw new ArgumentException("Nome é obrigatório.", nameof(nome)) : nome.Trim();
    }

    public string Nome { get; private set; }
}
