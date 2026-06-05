using Sigov.Domain.Saude;
using Xunit;

namespace Sigov.UnitTests.Saude;

public sealed class SaudeRulesTests
{
    [Fact] public void Unidade_exige_codigo_e_nome() => Assert.Throws<ArgumentException>(() => new UnidadeSaude("", "UBS"));
    [Fact] public void Paciente_exige_pessoa() => Assert.Throws<ArgumentException>(() => new Paciente(0, "PAC-1"));
    [Fact] public void Profissional_exige_pessoa() => Assert.Throws<ArgumentException>(() => new ProfissionalSaude(0, "PROF-1"));
    [Fact] public void Atendimento_exige_unidade_e_paciente() => Assert.Throws<ArgumentException>(() => new AtendimentoSaude(0, 1, "ATD-1"));
    [Fact] public void Atendimento_cancelado_nao_recebe_conduta() { var a = new AtendimentoSaude(1, 1, "ATD-1"); a.Cancelar(); Assert.Throws<InvalidOperationException>(() => a.RegistrarConduta("Alta")); }
    [Fact] public void Prontuario_pertence_a_paciente() => Assert.Throws<ArgumentException>(() => new Prontuario(0, "P-1"));
    [Fact] public void Agenda_exige_inicio_menor_que_fim() => Assert.Throws<ArgumentException>(() => new AgendaSaude(1, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddMinutes(-1)));
    [Fact] public void Dispensacao_exige_quantidade_positiva() => Assert.Throws<ArgumentException>(() => new FarmaciaDispensacao(1, 1, 0));
    [Fact] public void Estoque_nao_fica_negativo() { var e = new FarmaciaEstoque(1); Assert.Throws<InvalidOperationException>(() => e.Baixar(2)); }
    [Fact] public void Vacinacao_exige_paciente_vacina_dose() => Assert.Throws<ArgumentException>(() => new Vacinacao(1, "", "1", DateOnly.FromDateTime(DateTime.Today)));
    [Fact] public void Exame_concluido_exige_resultado() { var e = new LaboratorioExame(1, "Hemograma"); Assert.Throws<InvalidOperationException>(() => e.Concluir("{}")); }
    [Fact] public void Regulacao_exige_justificativa() => Assert.Throws<ArgumentException>(() => new RegulacaoSolicitacao(1, ""));
    [Fact] public void Cadastro_domiciliar_exige_endereco_ou_geo() => Assert.Throws<ArgumentException>(() => new AcsCadastroDomiciliar(null, null, null));
    [Fact] public void Visita_acs_exige_alvo() => Assert.Throws<ArgumentException>(() => new AcsVisita(1, null, null, null));
    [Fact] public void Coordenadas_invalidas_falham() => Assert.Throws<ArgumentException>(() => new AcsVisita(1, 1, null, null, 100, 0));
    [Fact] public void Sync_item_idempotente_no_lote() { var lote = new AcsSyncLote("L1"); lote.AdicionarItem(new AcsSyncItem("OFF1", "visita")); Assert.Throws<InvalidOperationException>(() => lote.AdicionarItem(new AcsSyncItem("OFF1", "visita"))); }
}
