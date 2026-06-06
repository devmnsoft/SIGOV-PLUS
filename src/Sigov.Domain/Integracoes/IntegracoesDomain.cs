using System.Text.Json;
using Sigov.Domain.Common;

namespace Sigov.Domain.Integracoes;

public enum ApiCredentialStatus { ATIVA, SUSPENSA, EXPIRADA, REVOGADA }
public enum IntegracaoTipo { GOVBR, ICP_BRASIL, TCE, ESFINGE, ESOCIAL, EDUCACENSO, ESUS, ABRASF_NFSE, DESIF, BANCO, PIX, WEBHOOK, API_EXTERNA, OUTROS }
public enum IntegracaoAmbiente { DEVELOPMENT, TEST, HOMOLOGACAO, PRODUCTION }
public enum IntegracaoStatus { ATIVA, INATIVA, ERRO, CONFIGURACAO_PENDENTE }
public enum WebhookStatus { RECEBIDO, PROCESSANDO, PROCESSADO, ERRO, REPROCESSAR, ENVIADO, FALHA }
public enum WebhookDirecao { ENTRADA, SAIDA }
public enum IdempotencyStatus { RESERVADA, PROCESSANDO, CONCLUIDA, EXPIRADA, ERRO }
public enum RemessaOficialTipo { TCE, ESFINGE, ESOCIAL, EDUCACENSO, ESUS, ABRASF_NFSE, DESIF, BANCO, PIX }
public enum RemessaOficialStatus { RASCUNHO, GERADA, ENVIADA_DEV, ENVIADA, CANCELADA, ERRO }
public enum CertificadoDigitalStatus { ATIVO, EXPIRADO, REVOGADO, BLOQUEADO }
public enum CertificadoDigitalTipo { A1, A3, NUVEM, ESTRUTURAL }

public sealed record ApiCredential(long TenantId, string Nome, string ClientId, string ApiKeyPrefix, string ApiKeyHash, ApiCredentialStatus Status)
{
    public static Result<ApiCredential> Create(long tenantId, string? nome, string? clientId, string? prefix, string? hash)
    {
        if (tenantId <= 0) return Result<ApiCredential>.Failure("Tenant é obrigatório.");
        if (string.IsNullOrWhiteSpace(nome)) return Result<ApiCredential>.Failure("Nome da credencial é obrigatório.");
        if (string.IsNullOrWhiteSpace(clientId)) return Result<ApiCredential>.Failure("ClientId é obrigatório.");
        if (string.IsNullOrWhiteSpace(hash) || hash.Contains("sigov_live_", StringComparison.OrdinalIgnoreCase) || hash.Contains("sigov_dev_", StringComparison.OrdinalIgnoreCase)) return Result<ApiCredential>.Failure("API key deve ser armazenada somente como hash seguro.");
        return Result<ApiCredential>.Success(new ApiCredential(tenantId, nome.Trim(), clientId.Trim(), prefix ?? string.Empty, hash, ApiCredentialStatus.ATIVA));
    }
}
public sealed record ApiCredentialScope(long ApiCredentialId, string Scope);
public sealed record IntegracaoSistema(long TenantId, string Codigo, string Nome, IntegracaoTipo Tipo, IntegracaoAmbiente Ambiente, IntegracaoStatus Status)
{
    public static Result<IntegracaoSistema> Create(long tenantId, string? codigo, string? nome, IntegracaoTipo tipo, IntegracaoAmbiente ambiente, bool fakeDevAdapter)
    {
        if (tenantId <= 0) return Result<IntegracaoSistema>.Failure("Tenant é obrigatório.");
        if (string.IsNullOrWhiteSpace(codigo) || string.IsNullOrWhiteSpace(nome)) return Result<IntegracaoSistema>.Failure("Código e nome da integração são obrigatórios.");
        if (ambiente == IntegracaoAmbiente.PRODUCTION && fakeDevAdapter) return Result<IntegracaoSistema>.Failure("Adapter fake/dev é bloqueado em Production.");
        return Result<IntegracaoSistema>.Success(new IntegracaoSistema(tenantId, codigo.Trim(), nome.Trim(), tipo, ambiente, IntegracaoStatus.ATIVA));
    }
}
public sealed record IntegracaoEndpoint(long TenantId, long IntegracaoSistemaId, string Nome, string Metodo, string Path);
public sealed record WebhookRecebido(long? TenantId, string Origem, string Evento, string Payload, string? IdempotencyKey)
{
    public static Result<WebhookRecebido> Create(long? tenantId, string? origem, string? evento, string? payload, string? idempotencyKey)
    {
        if (string.IsNullOrWhiteSpace(origem) || string.IsNullOrWhiteSpace(evento)) return Result<WebhookRecebido>.Failure("Origem e evento do webhook são obrigatórios.");
        if (string.IsNullOrWhiteSpace(payload) || !IsJson(payload)) return Result<WebhookRecebido>.Failure("Payload JSON do webhook é obrigatório.");
        return Result<WebhookRecebido>.Success(new WebhookRecebido(tenantId, origem.Trim(), evento.Trim(), payload, idempotencyKey));
    }
    private static bool IsJson(string payload) { try { JsonDocument.Parse(payload); return true; } catch (JsonException) { return false; } }
}
public sealed record WebhookEnviado(long TenantId, string Destino, string Url, string Evento, string Payload)
{
    public static Result<WebhookEnviado> Create(long tenantId, string? destino, string? url, string? evento, string? payload)
    {
        if (tenantId <= 0) return Result<WebhookEnviado>.Failure("Tenant é obrigatório.");
        if (string.IsNullOrWhiteSpace(destino) || string.IsNullOrWhiteSpace(url) || string.IsNullOrWhiteSpace(evento)) return Result<WebhookEnviado>.Failure("Destino, URL e evento são obrigatórios.");
        if (string.IsNullOrWhiteSpace(payload) || !url.StartsWith("http", StringComparison.OrdinalIgnoreCase)) return Result<WebhookEnviado>.Failure("Payload JSON e URL válida são obrigatórios.");
        return Result<WebhookEnviado>.Success(new WebhookEnviado(tenantId, destino.Trim(), url.Trim(), evento.Trim(), payload));
    }
}
public sealed record WebhookAssinatura(long TenantId, string Nome, string Algoritmo, string SecretHash);
public sealed record IdempotencyKey(long? TenantId, string Chave, DateTimeOffset ExpiresAt)
{
    public bool IsExpired(DateTimeOffset now) => ExpiresAt <= now;
    public bool Blocks(DateTimeOffset now) => !IsExpired(now);
}
public sealed record OutboxEvento(long Id, long? TenantId, string TipoEvento, int Tentativas, int MaxTentativas, bool DeadLetter)
{
    public OutboxEvento MarkFailure()
    {
        var nextTentativas = Tentativas + 1;
        return this with { Tentativas = nextTentativas, DeadLetter = nextTentativas >= MaxTentativas };
    }
}
public sealed record IntegracaoLog(string Direcao, string TipoEvento, string Status, string? RequestResumo)
{
    public static string MaskSecrets(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;
        var masked = value;
        foreach (var key in new[] { "api_key", "apiKey", "api_key_hash", "secret", "secret_hash", "client_secret", "token", "refresh_token", "authorization", "certificado", "senha_certificado", "chave_privada" })
        {
            masked = System.Text.RegularExpressions.Regex.Replace(masked, $"(?i)(\\\"?{key}\\\"?\\s*[:=]\\s*)\\\"?[^\\\",}} ]+", "$1***", System.Text.RegularExpressions.RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100));
        }
        return masked;
    }
}
public sealed record IntegracaoErro(long? TenantId, string Tipo, string Mensagem);
public sealed record IntegracaoJobExecucao(string JobNome, string Status, DateTimeOffset InicioAt);
public sealed record GovBrConfiguracao(long TenantId, string Ambiente, bool DevAdapterHabilitado);
public sealed record CertificadoDigital(long TenantId, string Nome, CertificadoDigitalTipo Tipo, DateOnly? ValidadeFim, string? StorageKey, string? Metadados)
{
    public static Result<CertificadoDigital> Create(long tenantId, string? nome, CertificadoDigitalTipo tipo, DateOnly? validadeFim, string? storageKey, string? metadados)
    {
        if (tenantId <= 0 || string.IsNullOrWhiteSpace(nome)) return Result<CertificadoDigital>.Failure("Tenant e nome do certificado são obrigatórios.");
        var combined = $"{storageKey} {metadados}";
        if (combined.Contains("senha", StringComparison.OrdinalIgnoreCase) || combined.Contains("private key", StringComparison.OrdinalIgnoreCase) || combined.Contains("BEGIN PRIVATE KEY", StringComparison.OrdinalIgnoreCase)) return Result<CertificadoDigital>.Failure("Senha ou chave privada de certificado não podem ser armazenadas em texto puro.");
        return Result<CertificadoDigital>.Success(new CertificadoDigital(tenantId, nome.Trim(), tipo, validadeFim, storageKey, metadados));
    }
    public bool PodeSerUsado(DateOnly hoje) => !ValidadeFim.HasValue || ValidadeFim.Value >= hoje;
}
public sealed record AssinadorDigital(long TenantId, string Nome, bool Estrutural);
public sealed record RemessaOficial(long TenantId, RemessaOficialTipo Tipo, string Numero, RemessaOficialStatus Status)
{
    public static Result<RemessaOficial> Create(long tenantId, RemessaOficialTipo tipo, string? numero)
    {
        if (tenantId <= 0) return Result<RemessaOficial>.Failure("Tenant é obrigatório.");
        if (string.IsNullOrWhiteSpace(numero)) return Result<RemessaOficial>.Failure("Número da remessa oficial é obrigatório.");
        return Result<RemessaOficial>.Success(new RemessaOficial(tenantId, tipo, numero.Trim(), RemessaOficialStatus.RASCUNHO));
    }
}
public sealed record RemessaOficialItem(long RemessaOficialId, string TipoItem, string Payload);
public sealed record IntegracaoEvento(long? TenantId, string TipoEvento, string Payload);
