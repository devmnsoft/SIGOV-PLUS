using FluentAssertions;
using Sigov.Domain.Agro;
using Xunit;
namespace Sigov.UnitTests.Agro;
public sealed class AgroTalhaoRulesTests { [Fact] public void Talhao_Exige_Area_Maior_Que_Zero() => FluentActions.Invoking(() => new Talhao(1,1,1,"T1","Talhão",0,null,null,"ATIVO")).Should().Throw<ArgumentException>(); }
