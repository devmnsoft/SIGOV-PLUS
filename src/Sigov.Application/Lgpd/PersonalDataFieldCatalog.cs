namespace Sigov.Application.Lgpd;

public static class PersonalDataFieldCatalog
{
    public static IReadOnlyCollection<string> Fields { get; } = new[] { "cpf", "cnpj", "email", "telefone", "endereco", "nome_social", "nis", "cartao_sus" };
}
