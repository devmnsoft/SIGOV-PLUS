using FluentAssertions;
using Sigov.Domain.Processos;
using Xunit;

namespace Sigov.UnitTests.Processos;

public sealed class DiarioOficialRulesTests
{
    [Fact] public void Diario_Publicado_Nao_Edita_Sem_Permissao() { var d = new DiarioOficialPublicacao(); d.Publicar(); Action act = () => d.ValidarEdicao(false); act.Should().Throw<InvalidOperationException>().WithMessage("*permissão administrativa*"); }
    [Fact] public void Ato_Oficial_Deve_Pertencer_A_Publicacao() { Action act = () => new AtoOficial(0); act.Should().Throw<ArgumentException>().WithMessage("*publicação*"); }
}
