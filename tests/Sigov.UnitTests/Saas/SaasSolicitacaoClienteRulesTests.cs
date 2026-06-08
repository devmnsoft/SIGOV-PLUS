using FluentAssertions;
using Sigov.Domain.Saas.Comercial;
using Xunit;

namespace Sigov.UnitTests.Saas;

public sealed class SaasSolicitacaoClienteRulesTests
{
    [Fact] public void Solicitacao_exige_organizacao() => new SaasSolicitacaoCliente(0, "", "Resp", "resp@sigov.local", SaasSolicitacaoStatus.Recebida).Validate().IsFailure.Should().BeTrue();
    [Fact] public void Solicitacao_exige_responsavel() => new SaasSolicitacaoCliente(0, "Org", "", "resp@sigov.local", SaasSolicitacaoStatus.Recebida).Validate().IsFailure.Should().BeTrue();
    [Fact] public void Solicitacao_exige_email_valido() => new SaasSolicitacaoCliente(0, "Org", "Resp", "email-invalido", SaasSolicitacaoStatus.Recebida).Validate().IsFailure.Should().BeTrue();
    [Fact] public void Solicitacao_convertida_nao_converte_novamente() => new SaasSolicitacaoCliente(0, "Org", "Resp", "resp@sigov.local", SaasSolicitacaoStatus.ConvertidaTenant).Converter().IsFailure.Should().BeTrue();
}
