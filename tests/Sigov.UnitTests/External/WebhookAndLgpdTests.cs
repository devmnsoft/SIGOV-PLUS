using FluentAssertions;
using Sigov.Application.External;

namespace Sigov.UnitTests.External;

public sealed class WebhookAndLgpdTests
{
    [Fact]
    public void WebhookAssinatura_UsaHmacSha256()
    {
        var assinatura = WebhookService.AssinarPayload("segredo", "{\"evento\":\"protocolo.criado\"}");
        assinatura.Should().StartWith("sha256=");
        assinatura.Length.Should().Be(71);
    }

    [Theory]
    [InlineData("123.456.789-09", "*******8909")]
    [InlineData("12.345.678/0001-99", "**********0199")]
    public void MascaramentoLgpd_NaoExpoeDocumentoCompleto(string documento, string esperado)
    {
        LgpdExternalMaskingService.MascararDocumento(documento).Should().Be(esperado);
    }

    [Fact]
    public void EventosSuportados_IncluemFluxosDaSprint()
    {
        WebhookService.EventosSuportados.Should().Contain(new[] { "documento.assinado", "obra.medicao_registrada", "sla.vencido" });
    }
}
