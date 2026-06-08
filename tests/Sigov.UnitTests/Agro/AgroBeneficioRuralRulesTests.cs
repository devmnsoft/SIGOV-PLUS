using FluentAssertions;
using Sigov.Domain.Agro;
using Sigov.Domain.Agro.Enums;
using Xunit;

namespace Sigov.UnitTests.Agro;

public sealed class AgroBeneficioRuralRulesTests
{
    [Fact] public void Beneficio_exige_nome() => FluentActions.Invoking(() => new BeneficioRural(1,1,"BEN","",AgroBeneficioTipo.OUTROS)).Should().Throw<ArgumentException>();
    [Fact] public void Valor_referencia_negativo_falha() => FluentActions.Invoking(() => new BeneficioRural(1,1,"BEN","Benefício",AgroBeneficioTipo.OUTROS,-1)).Should().Throw<ArgumentException>();
    [Fact] public void Concessao_exige_produtor() => FluentActions.Invoking(() => new BeneficioRuralConcessao(1,1,1,1,0,"N",1,1,AgroBeneficioStatus.SOLICITADO)).Should().Throw<ArgumentException>();
    [Fact] public void Concessao_autorizada_exige_usuario() => FluentActions.Invoking(() => new BeneficioRuralConcessao(1,1,1,1,1,"N",1,1,AgroBeneficioStatus.AUTORIZADO)).Should().Throw<ArgumentException>();
    [Fact] public void Concessao_cancelada_nao_pode_ser_entregue() { var c = new BeneficioRuralConcessao(1,1,1,1,1,"N",1,1,AgroBeneficioStatus.CANCELADO); FluentActions.Invoking(() => c.Entregar(1, DateTimeOffset.UtcNow)).Should().Throw<InvalidOperationException>(); }
}
