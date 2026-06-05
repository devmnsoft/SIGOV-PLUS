using FluentAssertions; using Sigov.Domain.Saneamento; using Xunit;
namespace Sigov.UnitTests.Saneamento; public sealed class ConsumidorSaneamentoRulesTests { [Fact] public void Consumidor_Exige_Pessoa() => FluentActions.Invoking(() => new SaneamentoConsumidor(1,1,0,"CON")).Should().Throw<ArgumentException>(); }
