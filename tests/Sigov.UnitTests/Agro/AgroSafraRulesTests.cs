using FluentAssertions;
using Sigov.Domain.Agro;
using Xunit;
namespace Sigov.UnitTests.Agro;
public sealed class AgroSafraRulesTests { [Fact] public void Safra_Ano_Fim_Nao_Pode_Ser_Menor_Que_Inicio() => FluentActions.Invoking(() => new Safra(1,1,null,"S","Safra",2026,2025,null,null,"ATIVA")).Should().Throw<ArgumentException>(); }
