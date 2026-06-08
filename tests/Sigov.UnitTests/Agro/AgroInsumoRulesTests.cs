using FluentAssertions;
using Sigov.Domain.Agro;
using Sigov.Domain.Agro.Enums;
using Xunit;

namespace Sigov.UnitTests.Agro;

public sealed class AgroInsumoRulesTests
{
    [Fact] public void Insumo_exige_unidade_medida() => FluentActions.Invoking(() => new Insumo(1,1,"INS","Insumo",AgroInsumoTipo.OUTROS," ")).Should().Throw<ArgumentException>();
    [Fact] public void Distribuicao_exige_quantidade_maior_que_zero() => FluentActions.Invoking(() => new DistribuicaoInsumo(1,1,1,1,1,"N",0,null)).Should().Throw<ArgumentException>();
}
