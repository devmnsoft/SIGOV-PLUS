using FluentAssertions; using Sigov.Domain.Saneamento; using Xunit;
namespace Sigov.UnitTests.Saneamento; public sealed class LigacaoSaneamentoRulesTests { [Fact] public void Ligacao_Exige_Numero() => FluentActions.Invoking(() => new SaneamentoLigacao(1,1,1," ")).Should().Throw<ArgumentException>(); }
