using FluentAssertions;
using Sigov.Domain.Processos;
using Xunit;

namespace Sigov.UnitTests.Processos;

public sealed class ProtocoloRulesTests
{
    [Fact] public void Protocolo_Nao_Converte_Duas_Vezes() { var p = new ProtocoloAtendimento(); p.Converter(10); Action act = () => p.Converter(11); act.Should().Throw<InvalidOperationException>().WithMessage("*única vez*"); }
}
