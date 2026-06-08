using FluentAssertions; using Sigov.Domain.Agro; using Sigov.Domain.Agro.Enums; using Xunit;
namespace Sigov.UnitTests.Agro; public sealed class AgroFeiraRulesTests{[Fact] public void Feira_exige_nome_e_local()=>FluentActions.Invoking(()=>new FeiraRural(1,1,"F1","","",AgroFeiraSituacao.ATIVA)).Should().Throw<ArgumentException>();}
