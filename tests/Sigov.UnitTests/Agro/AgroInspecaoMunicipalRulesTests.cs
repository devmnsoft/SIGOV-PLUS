using FluentAssertions; using Sigov.Domain.Agro; using Sigov.Domain.Agro.Enums; using Xunit;
namespace Sigov.UnitTests.Agro; public sealed class AgroInspecaoMunicipalRulesTests{[Fact] public void Inspecao_exige_data()=>FluentActions.Invoking(()=>new InspecaoMunicipal(1,1,1,"I1",default,AgroInspecaoResultado.APROVADA,false,null)).Should().Throw<ArgumentException>();}
