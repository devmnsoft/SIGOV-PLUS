using FluentAssertions; using Sigov.Domain.Agro; using Sigov.Domain.Agro.Enums; using Xunit;
namespace Sigov.UnitTests.Agro; public sealed class AgroAgroindustriaRulesTests{[Fact] public void Agroindustria_exige_nome_e_atividade()=>FluentActions.Invoking(()=>new Agroindustria(1,1,"A1","","",AgroAgroindustriaSituacao.ATIVA)).Should().Throw<ArgumentException>();}
