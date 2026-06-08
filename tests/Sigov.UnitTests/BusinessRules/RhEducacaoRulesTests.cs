using FluentAssertions;
using Sigov.Application.BusinessRules;
using Xunit;

namespace Sigov.UnitTests.BusinessRules;

public sealed class RhEducacaoRulesTests
{
    [Fact]
    public void Catalogo_Rh_Deve_Conter_Regras_Completas_Do_Lote_3B()
    {
        var rules = new BusinessRuleCatalog().GetRulesByModule("RH").Select(rule => rule.Description).ToArray();

        rules.Should().Contain("Servidor exige pessoa e matrícula.");
        rules.Should().Contain("Matrícula de servidor é única por tenant e entidade.");
        rules.Should().Contain("Folha exige competência válida.");
        rules.Should().Contain("Folha mês entre 1 e 13.");
        rules.Should().Contain("Folha fechada não recebe lançamento comum.");
        rules.Should().Contain("Lançamento de folha não aceita valor negativo.");
        rules.Should().Contain("Férias fim >= início.");
        rules.Should().Contain("Férias não conflitam com afastamento ativo.");
        rules.Should().Contain("Afastamento fim >= início.");
        rules.Should().Contain("Saúde ocupacional é dado sensível.");
        rules.Should().Contain("Portal do servidor só mostra dados autorizados.");
    }

    [Fact]
    public void Catalogo_Educacao_Deve_Conter_Regras_Completas_Do_Lote_3B()
    {
        var rules = new BusinessRuleCatalog().GetRulesByModule("Educacao").Select(rule => rule.Description).ToArray();

        rules.Should().Contain("Escola exige código e nome.");
        rules.Should().Contain("Ano letivo fim >= início.");
        rules.Should().Contain("Turma exige capacidade positiva.");
        rules.Should().Contain("Vagas ocupadas não ultrapassam capacidade.");
        rules.Should().Contain("Aluno exige pessoa.");
        rules.Should().Contain("Matrícula exige aluno, turma, escola e ano letivo.");
        rules.Should().Contain("Matrícula ativa ocupa vaga.");
        rules.Should().Contain("Matrícula cancelada libera vaga.");
        rules.Should().Contain("Frequência exige turma e matrícula ativa.");
        rules.Should().Contain("Nota não pode ser negativa.");
        rules.Should().Contain("Nota não pode ultrapassar valor máximo.");
        rules.Should().Contain("Pré-matrícula convertida não converte novamente.");
        rules.Should().Contain("Dados de aluno e responsável são dados pessoais.");
        rules.Should().Contain("Dados sensíveis do aluno devem ser mascarados.");
    }
}
