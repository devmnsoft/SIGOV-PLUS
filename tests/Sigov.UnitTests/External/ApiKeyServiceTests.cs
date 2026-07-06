using FluentAssertions;
using Sigov.Application.External;

namespace Sigov.UnitTests.External;

public sealed class ApiKeyServiceTests
{
    [Fact]
    public void HashToken_NaoRetornaTextoClaro_EValidaEmTempoConstante()
    {
        var token = ApiKeyService.GerarToken();
        var hash = ApiKeyService.HashToken(token);
        hash.Should().NotBe(token);
        ApiKeyService.Validar(token, hash).Should().BeTrue();
        ApiKeyService.Validar(token + "x", hash).Should().BeFalse();
    }

    [Fact]
    public void EscoposPadrao_IncluemFluxosExternos()
    {
        ApiKeyService.EscoposPadrao.Should().Contain(new[] { "protocolos.read", "webhooks.manage", "mobile.sync", "assinaturas.write", "bi.read" });
        ApiKeyService.TemEscopo(ApiKeyService.EscoposPadrao, "mobile.sync").Should().BeTrue();
    }
}
