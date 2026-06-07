namespace Sigov.Application.Lgpd;

public static class SensitiveFieldCatalog
{
    public static IReadOnlyCollection<string> Fields { get; } = new[] { "saude", "prontuario", "resultado_exame", "alergias", "condicoes_cronicas", "vulnerabilidades", "dados_sensiveis_json", "parecer_social", "afastamento", "dados_bancarios" };
    public static IReadOnlyCollection<string> SecretFields { get; } = new[] { "token", "secret", "api_key", "senha", "password", "certificado", "private_key", "connection_string" };
}
