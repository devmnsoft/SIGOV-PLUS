using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Sigov.Application.External;

public static class ApiKeyService
{
    public static readonly string[] EscoposPadrao =
    {
        "protocolos.read", "protocolos.write", "documentos.read", "documentos.write",
        "tarefas.read", "tarefas.write", "notificacoes.read", "webhooks.manage",
        "mobile.sync", "assinaturas.read", "assinaturas.write", "bi.read"
    };

    public static string GerarToken() => $"sigov_{Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant()}";
    public static string Prefixo(string token) => string.IsNullOrWhiteSpace(token) ? string.Empty : token[..Math.Min(12, token.Length)];
    public static string HashToken(string token)
    {
        using var sha = SHA256.Create();
        return Convert.ToHexString(sha.ComputeHash(Encoding.UTF8.GetBytes(token))).ToLowerInvariant();
    }
    public static bool Validar(string token, string hash) => CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(HashToken(token)), Encoding.UTF8.GetBytes(hash));
    public static bool TemEscopo(IEnumerable<string> escopos, string requerido) => escopos.Any(e => string.Equals(e, requerido, StringComparison.OrdinalIgnoreCase));
}

public static class WebhookService
{
    public static readonly string[] EventosSuportados =
    {
        "protocolo.criado", "protocolo.tramitado", "documento.criado", "documento.assinado",
        "tarefa.criada", "tarefa.concluida", "contrato.criado", "obra.medicao_registrada",
        "manifestacao.recebida", "chamado.aberto", "sla.vencido"
    };

    public static string AssinarPayload(string secret, string payload)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        return "sha256=" + Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(payload))).ToLowerInvariant();
    }
}

public static class LgpdExternalMaskingService
{
    public static string MascararDocumento(string? valor)
    {
        if (string.IsNullOrWhiteSpace(valor)) return string.Empty;
        var digits = new string(valor.Where(char.IsDigit).ToArray());
        if (digits.Length <= 4) return "****";
        return new string('*', Math.Max(0, digits.Length - 4)) + digits[^4..];
    }

    public static object PayloadSeguro(object payload) => JsonSerializer.Deserialize<object>(JsonSerializer.Serialize(payload))!;
}

public sealed record ExternalApiEnvelope(bool Success, string Message, object? Data, string[] Errors, string CorrelationId)
{
    public static ExternalApiEnvelope Ok(object? data, string correlationId, string message = "Operação realizada com sucesso.") => new(true, message, data, Array.Empty<string>(), correlationId);
    public static ExternalApiEnvelope Fail(string error, string correlationId) => new(false, "Não foi possível concluir a operação.", null, new[] { error }, correlationId);
}
