namespace Sigov.Domain.Saneamento;

public sealed partial class UnidadeConsumidora
{
    public UnidadeConsumidora(string nome)
    {
        Nome = string.IsNullOrWhiteSpace(nome) ? throw new ArgumentException("Nome é obrigatório.", nameof(nome)) : nome.Trim();
        TenantId = 1;
        EntidadeId = 1;
        ConsumidorId = 1;
        CodigoUnidade = Nome;
    }

    public string Nome { get; private set; }
}
