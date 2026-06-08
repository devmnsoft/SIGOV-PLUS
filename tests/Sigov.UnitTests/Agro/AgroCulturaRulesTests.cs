using FluentAssertions;
using Sigov.Domain.Agro;
using Xunit;
namespace Sigov.UnitTests.Agro;
public sealed class AgroCulturaRulesTests { [Fact] public void Cultura_Exige_Codigo_E_Nome(){ FluentActions.Invoking(() => new Cultura(1,1,"","Milho","GRAOS",null,"kg")).Should().Throw<ArgumentException>(); FluentActions.Invoking(() => new Cultura(1,1,"MILHO","","GRAOS",null,"kg")).Should().Throw<ArgumentException>(); } }
