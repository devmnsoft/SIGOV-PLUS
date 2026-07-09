namespace Sigov.Domain.Saneamento;

public sealed partial class UnidadeConsumidora
{
    public UnidadeConsumidora(string nome)
    {
        Nome = string.IsNullOrWhiteSpace(nome) ? throw new ArgumentException("Nome é obrigatório.", nameof(nome)) : nome.Trim();
        TenantId = 0;
        EntidadeId = 0;
        ConsumidorId = 0;
        CodigoUnidade = Nome;
    }

    public string Nome { get; private set; }
}
