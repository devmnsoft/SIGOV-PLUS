using FluentAssertions;
using Sigov.Domain.Educacao;
using Xunit;

namespace Sigov.UnitTests.Educacao;

public sealed class EscolaRulesTests
{
    [Fact] public void Escola_Exige_Codigo_E_Nome() { Assert.Throws<ArgumentException>(() => new Escola(1, 1, "", "Escola")); Assert.Throws<ArgumentException>(() => new Escola(1, 1, "E1", "")); }
}

public sealed class AnoLetivoRulesTests
{
    [Fact] public void Ano_Letivo_Nao_Aceita_Fim_Antes_Do_Inicio() => Assert.Throws<InvalidOperationException>(() => new AnoLetivo(1, 1, 1, 2026, new DateOnly(2026, 2, 1), new DateOnly(2026, 1, 1)));
}

public sealed class TurmaRulesTests
{
    [Fact] public void Turma_Exige_Capacidade_Positiva() => Assert.Throws<InvalidOperationException>(() => NovaTurma(0));
    [Fact] public void Turma_Nao_Permite_Exceder_Vagas() { var t = NovaTurma(1); t.MatricularAluno(); Assert.Throws<InvalidOperationException>(() => t.MatricularAluno()); }
    private static Turma NovaTurma(int capacidade) => new(1, 1, 1, 1, 1, 1, 1, "T1", "Turma", Turno.Matutino, capacidade);
}

public sealed class AlunoRulesTests
{
    [Fact] public void Aluno_Exige_Pessoa() => Assert.Throws<ArgumentException>(() => new Aluno(1, 1, 0, "A1"));
    [Fact] public void Dados_Sensiveis_Sao_Protegidos() => new Aluno(1, 1, 10, "A1").DadosSensiveisProtegidos.Should().Be("***");
}

public sealed class MatriculaRulesTests
{
    [Fact] public void Matricula_Exige_Aluno_Turma_Escola_Ano() { var t = Turma(); Assert.Throws<ArgumentException>(() => new Matricula(1, 1, 1, 0, 1, 1, t, "M1")); }
    [Fact] public void Aluno_Nao_Tem_Duas_Matriculas_Ativas_Na_Mesma_Escola_Ano() { var t = Turma(); var m = new Matricula(1, 1, 1, 1, 1, 1, t, "M1"); Assert.Throws<InvalidOperationException>(() => Matricula.GarantirSemMatriculaAtivaDuplicada(new[] { m }, 1, 1, 1)); }
    [Fact] public void Matricula_Cancelada_Nao_Recebe_Frequencia() { var t = Turma(); var m = new Matricula(1, 1, 1, 1, 1, 1, t, "M1"); m.Cancelar(t); Assert.Throws<InvalidOperationException>(() => new DiarioFrequencia(1, 1, 1, m, m.TurmaId, m.AlunoId, DateOnly.FromDateTime(DateTime.Today), true)); }
    private static Turma Turma() => new(1, 1, 1, 1, 1, 1, 1, "T1", "Turma", Turno.Matutino, 2);
}

public sealed class FrequenciaRulesTests
{
    [Fact] public void Frequencia_Exige_Aluno_Matriculado_Na_Turma() { var t = new Turma(1, 1, 1, 1, 1, 1, 1, "T1", "Turma", Turno.Matutino, 2); var m = new Matricula(1, 1, 1, 1, 1, 1, t, "M1"); Assert.Throws<InvalidOperationException>(() => new DiarioFrequencia(1, 1, 1, m, 999, 1, DateOnly.FromDateTime(DateTime.Today), true)); }
}

public sealed class AvaliacaoRulesTests
{
    [Fact] public void Nota_Nao_Pode_Ser_Negativa() { var a = new Avaliacao(1, 1, 1, 1, "MAT", "Prova", DateOnly.FromDateTime(DateTime.Today)); Assert.Throws<InvalidOperationException>(() => new Nota(1, 1, 1, a, 1, -1m)); }
    [Fact] public void Nota_Nao_Ultrapassa_Valor_Maximo() { var a = new Avaliacao(1, 1, 1, 1, "MAT", "Prova", DateOnly.FromDateTime(DateTime.Today), 10m); Assert.Throws<InvalidOperationException>(() => new Nota(1, 1, 1, a, 1, 11m)); }
    [Fact] public void Nota_Usa_Decimal() => typeof(Nota).GetProperty(nameof(Nota.Valor))!.PropertyType.Should().Be(typeof(decimal));
}

public sealed class PreMatriculaRulesTests
{
    [Fact] public void Pre_Matricula_Convertida_Nao_Converte_Novamente() { var p = new PreMatriculaInscricao(1, 1, 1, 1, "PRE-2026-000001", 2026, EtapaEnsino.EnsinoFundamental); p.Converter(); Assert.Throws<InvalidOperationException>(() => p.Converter()); }
}

public sealed class EducacensoRulesTests
{
    [Fact] public void Educacenso_Guarda_Payload_E_Status() { var r = new EducacensoRegistro(1, 1, 1, "ALUNO", "{\"id\":1}"); r.Payload.Should().Contain("id"); r.Status.Should().Be(EducacensoStatus.Pendente); }
}
