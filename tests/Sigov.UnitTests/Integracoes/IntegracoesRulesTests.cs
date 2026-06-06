using Sigov.Application.Integracoes;
using Sigov.Domain.Integracoes;
using Xunit;

namespace Sigov.UnitTests.Integracoes;

public sealed class IntegracoesRulesTests
{
    [Fact]
    public void ApiCredential_ExigeNomeClientIdEHashSeguro()
    {
        Assert.True(ApiCredential.Create(1, "App", "client", "abc", "PBKDF2-SHA256-100000$salt$hash").IsSuccess);
        Assert.True(ApiCredential.Create(1, "App", "client", "abc", "sigov_live_abc_secret").IsFailure);
        Assert.True(ApiCredential.Create(1, "", "client", "abc", "hash").IsFailure);
    }

    [Fact]
    public void ApiKeyHasher_GeraHashENaoSalvaTextoPuro()
    {
        var hasher = new ApiKeyHasher();
        var key = hasher.GenerateApiKey(false, out var prefix);
        var hash = hasher.Hash(key);
        Assert.StartsWith("sigov_dev_", key);
        Assert.Contains(prefix, key);
        Assert.DoesNotContain(key, hash, StringComparison.Ordinal);
        Assert.True(hasher.Verify(key, hash));
        Assert.False(hasher.Verify(key + "x", hash));
    }

    [Fact]
    public void WebhookSignature_ValidaAssinaturaERejeitaReplayAntigo()
    {
        var service = new WebhookSignatureService();
        var timestamp = DateTimeOffset.UtcNow;
        var signature = service.Sign("{}", "segredo-dev", timestamp);
        Assert.True(service.Validate("{}", "segredo-dev", signature, timestamp, TimeSpan.FromMinutes(5), timestamp.AddMinutes(1)));
        Assert.False(service.Validate("{}", "segredo-dev", signature + "x", timestamp, TimeSpan.FromMinutes(5), timestamp.AddMinutes(1)));
        Assert.False(service.Validate("{}", "segredo-dev", signature, timestamp, TimeSpan.FromMinutes(5), timestamp.AddMinutes(6)));
    }

    [Fact]
    public void WebhookRecebido_ExigeOrigemEventoPayload()
    {
        Assert.True(WebhookRecebido.Create(1, "origem", "evento", "{}", "idem").IsSuccess);
        Assert.True(WebhookRecebido.Create(1, "", "evento", "{}", null).IsFailure);
        Assert.True(WebhookRecebido.Create(1, "origem", "evento", "texto", null).IsFailure);
    }

    [Fact]
    public void Outbox_ExcedendoTentativasVaiParaDeadLetter()
    {
        var evento = new OutboxEvento(10, 1, "WebhookEnviado", 4, 5, false).MarkFailure();
        Assert.Equal(5, evento.Tentativas);
        Assert.True(evento.DeadLetter);
    }

    [Fact]
    public void Certificado_ExpiradoNaoPodeSerUsadoESegredosSaoBloqueados()
    {
        var cert = CertificadoDigital.Create(1, "Cert", CertificadoDigitalTipo.ESTRUTURAL, DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1)), "vault/cert", "{}");
        Assert.True(cert.IsSuccess);
        Assert.False(cert.Value!.PodeSerUsado(DateOnly.FromDateTime(DateTime.UtcNow)));
        Assert.True(CertificadoDigital.Create(1, "Cert", CertificadoDigitalTipo.A1, null, "senha=123", "{}").IsFailure);
    }

    [Fact]
    public void Logs_MascaramSegredos()
    {
        var masked = IntegracaoLog.MaskSecrets("{\"api_key\":\"abc\",\"client_secret\":\"def\",\"token\":\"ghi\"}");
        Assert.DoesNotContain("abc", masked);
        Assert.DoesNotContain("def", masked);
        Assert.DoesNotContain("ghi", masked);
        Assert.Contains("***", masked);
    }

    [Fact]
    public void IdempotencyKey_ExpiradaNaoBloqueiaNovaOperacao()
    {
        var key = new IdempotencyKey(1, "idem", DateTimeOffset.UtcNow.AddMinutes(-1));
        Assert.False(key.Blocks(DateTimeOffset.UtcNow));
    }
}
