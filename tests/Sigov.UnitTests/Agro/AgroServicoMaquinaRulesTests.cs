using FluentAssertions;
using Sigov.Domain.Agro;
using Sigov.Domain.Agro.Enums;
using Xunit;

namespace Sigov.UnitTests.Agro;

public sealed class AgroServicoMaquinaRulesTests
{
    [Fact] public void Servico_exige_produtor() => FluentActions.Invoking(() => new ServicoMaquinaRural(1,1,1,0,"S",AgroServicoMaquinaTipo.OUTROS,AgroServicoMaquinaStatus.SOLICITADO)).Should().Throw<ArgumentException>();
    [Fact] public void Servico_executado_exige_data() => FluentActions.Invoking(() => new ServicoMaquinaRural(1,1,1,1,"S",AgroServicoMaquinaTipo.OUTROS,AgroServicoMaquinaStatus.EXECUTADO)).Should().Throw<ArgumentException>();
    [Fact] public void Horimetro_final_menor_que_inicial_falha() => FluentActions.Invoking(() => new ServicoMaquinaRural(1,1,1,1,"S",AgroServicoMaquinaTipo.OUTROS,AgroServicoMaquinaStatus.EXECUTADO,DateOnly.FromDateTime(DateTime.UtcNow),null,null,10,9)).Should().Throw<ArgumentException>();
    [Fact] public void Servico_cancelado_nao_executa() { var s = new ServicoMaquinaRural(1,1,1,1,"S",AgroServicoMaquinaTipo.OUTROS,AgroServicoMaquinaStatus.CANCELADO); FluentActions.Invoking(() => s.Executar(DateOnly.FromDateTime(DateTime.UtcNow))).Should().Throw<InvalidOperationException>(); }
}
