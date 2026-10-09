using FluentAssertions;
using Sigov.Domain.Rh;
using System.Text.Json;
using Xunit;

namespace Sigov.UnitTests.Rh;

public sealed class RhRulesTests
{
    [Fact]
    public void Servidor_Exige_Tenant_Matricula_Nome_E_Cpf()
    {
        Assert.Throws<ArgumentException>(() => new Servidor(0, "1", "Maria", "00000000000", new DateOnly(1990, 1, 1)));
        Assert.Throws<ArgumentException>(() => new Servidor(1, "", "Maria", "00000000000", new DateOnly(1990, 1, 1)));
        Assert.Throws<ArgumentException>(() => new Servidor(1, "1", "", "00000000000", new DateOnly(1990, 1, 1)));
        Assert.Throws<ArgumentException>(() => new Servidor(1, "1", "Maria", "", new DateOnly(1990, 1, 1)));
    }

    [Fact]
    public void Servidor_Classifica_Dados_Pessoais_Como_Lgpd_Sensiveis()
    {
        var servidor = new Servidor(1, "MAT-1", "Maria Silva", "00000000000", new DateOnly(1990, 1, 1));
        servidor.ClassificacaoLgpd.Should().Be("dados_pessoais_sensiveis");
    }

    [Fact]
    public void Folha_Nao_Aceita_Mes_Invalido()
    {
        Assert.Throws<ArgumentException>(() => new Folha(1, 2026, 14, "mensal"));
    }


    [Fact]
    public void Cargo_Exige_Codigo_E_Nome()
    {
        Assert.Throws<ArgumentException>(() => new Cargo(1, "", "Analista"));
        Assert.Throws<ArgumentException>(() => new Cargo(1, "ANL", ""));
    }

    [Fact]
    public void Ferias_E_Afastamento_Nao_Aceitam_Fim_Antes_Do_Inicio_Nas_Regras_Tipadas()
    {
        var inicio = new DateOnly(2026, 3, 10);
        var fim = new DateOnly(2026, 3, 1);
        (fim < inicio).Should().BeTrue();
    }

    [Fact]
    public void Registro_Principal_Usa_Soft_Delete_Com_Auditoria()
    {
        var cargo = new Cargo(1, "TEC", "Técnico Administrativo");
        cargo.Excluir(7);
        cargo.IsDeleted.Should().BeTrue();
        cargo.Ativo.Should().BeFalse();
        cargo.DeletedBy.Should().Be(7);
        cargo.DeletedAt.Should().NotBeNull();
    }

    // ==== RC-EVO-RH §4: engine de apuração real de ponto =========================

    private static readonly DateOnly PontoSeg = new(2026, 10, 5); // segunda-feira
    private static readonly TimeZoneInfo ZonaBrasilia = TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo");

    private static JornadaPontoRegra JornadaPadrao => new(1, "Padrão", 40m, new TimeOnly(8, 0), new TimeOnly(17, 0), 10, new[] { 1, 2, 3, 4, 5 });

    // BRT em 2026 é UTC-3 fixo (sem horário de verão desde 2019).
    private static BatidaPonto Batida(long id, DateOnly data, TimeOnly horaLocal, PontoTipo tipo)
        => new(id, new DateTimeOffset(data.ToDateTime(horaLocal), TimeSpan.FromHours(-3)), tipo);

    [Fact]
    public void Apuracao_Dia_Completo_Com_Intervalo_Calcula_Minutos_E_TimeSpan_Sem_Pendencias()
    {
        var resultado = PontoApuracaoEngine.Calcular(1, PontoSeg, PontoSeg, ZonaBrasilia, new[]
        {
            Batida(1, PontoSeg, new TimeOnly(8, 0), PontoTipo.Entrada),
            Batida(2, PontoSeg, new TimeOnly(12, 0), PontoTipo.IntervaloInicio),
            Batida(3, PontoSeg, new TimeOnly(13, 0), PontoTipo.IntervaloFim),
            Batida(4, PontoSeg, new TimeOnly(17, 0), PontoTipo.Saida)
        }, _ => JornadaPadrao);

        resultado.PendenciasGlobais.Should().BeEmpty();
        resultado.DiasUteisPrevistos.Should().Be(1);
        resultado.DiasFalta.Should().Be(0);
        resultado.TotalIntervaloMinutos.Should().Be(60);
        resultado.TotalTrabalhadoMinutos.Should().Be(480);
        resultado.TotalTrabalhado.Should().Be(TimeSpan.FromMinutes(480));
        resultado.MemoriaPorDia.Should().HaveCount(1);
        resultado.MemoriaPorDia[0].EntradaEfetiva.Should().Be(new TimeOnly(8, 0));
        resultado.MemoriaPorDia[0].SaidaEfetiva.Should().Be(new TimeOnly(17, 0));
        resultado.VersaoRegras.Should().Be("RH-APURACAO-2");
        resultado.FusoHorarioOperacao.Should().Be(ZonaBrasilia.Id);
    }

    [Fact]
    public void Apuracao_Atraso_Respeita_Tolerancia_Da_Jornada()
    {
        var resultado = PontoApuracaoEngine.Calcular(1, PontoSeg, PontoSeg.AddDays(1), ZonaBrasilia, new[]
        {
            Batida(1, PontoSeg, new TimeOnly(8, 5), PontoTipo.Entrada),   // 5 min > dentro da tolerância de 10
            Batida(2, PontoSeg, new TimeOnly(17, 0), PontoTipo.Saida),
            Batida(3, PontoSeg.AddDays(1), new TimeOnly(8, 25), PontoTipo.Entrada), // 25 - 10 = 15 min
            Batida(4, PontoSeg.AddDays(1), new TimeOnly(17, 0), PontoTipo.Saida)
        }, _ => JornadaPadrao);

        resultado.TotalAtrasoMinutos.Should().Be(15);
        resultado.MemoriaPorDia[0].AtrasoMinutos.Should().Be(0);
        resultado.MemoriaPorDia[1].AtrasoMinutos.Should().Be(15);
        resultado.TotalAtraso.Should().Be(TimeSpan.FromMinutes(15));
    }

    [Fact]
    public void Apuracao_Virada_De_Dia_Attribui_Saida_Do_Dia_Seguinte_Ao_Turno_Noturno()
    {
        var noturno = new JornadaPontoRegra(2, "Noturno", 44m, new TimeOnly(18, 0), new TimeOnly(6, 0), 0, new[] { 1, 2, 3, 4, 5 });
        var resultado = PontoApuracaoEngine.Calcular(1, PontoSeg, PontoSeg, ZonaBrasilia, new[]
        {
            Batida(1, PontoSeg, new TimeOnly(18, 5), PontoTipo.Entrada),
            Batida(2, PontoSeg.AddDays(1), new TimeOnly(6, 5), PontoTipo.Saida) // sai na manhã de terça
        }, _ => noturno);

        resultado.PendenciasGlobais.Should().BeEmpty();
        resultado.DiasFalta.Should().Be(0);
        resultado.TotalTrabalhadoMinutos.Should().Be(720);
        resultado.TotalTrabalhado.Should().Be(TimeSpan.FromHours(12));
        resultado.MemoriaPorDia[0].AtrasoMinutos.Should().Be(5);
    }

    [Fact]
    public void Apuracao_Virada_De_Dia_Pareia_Saida_Com_Turno_Aberto_E_Marca_Batida_Orfa()
    {
        var noturno = new JornadaPontoRegra(2, "Noturno", 44m, new TimeOnly(18, 0), new TimeOnly(6, 0), 0, new[] { 1, 2, 3, 4, 5 });
        // RC-EVO-B §3.1 (pareamento cronológico): a saída de quinta 06:00 fecha legitimamente o turno
        // aberto na quarta 18:00; a saída órfã de quarta 06:00 pertence a turno aberto antes do período
        // de apuração e fica registrada como ENTRADA_AUSENTE no dia dela — nada é atribuído às cegas.
        var resultado = PontoApuracaoEngine.Calcular(1, PontoSeg.AddDays(2), PontoSeg.AddDays(3), ZonaBrasilia, new[]
        {
            Batida(1, PontoSeg.AddDays(2), new TimeOnly(18, 0), PontoTipo.Entrada),
            Batida(2, PontoSeg.AddDays(2), new TimeOnly(6, 0), PontoTipo.Saida),
            Batida(3, PontoSeg.AddDays(3), new TimeOnly(6, 0), PontoTipo.Saida)
        }, _ => noturno);

        resultado.MemoriaPorDia[0].Pendencias.Should().Contain("ENTRADA_AUSENTE"); // saída órfã de quarta registrada
        resultado.MemoriaPorDia[1].Pendencias.Should().BeEmpty(); // saída de quinta consumida pelo turno de quarta
        resultado.MemoriaPorDia[0].TrabalhadoMinutos.Should().Be(720); // tempo atribuído ao dia de abertura do período
        resultado.TotalTrabalhadoMinutos.Should().Be(720); // somente o turno pareado pela sequência válida
    }

    [Fact]
    public void Apuracao_Falta_Plena_No_Dia_Util_Gera_Ausencia_Sem_Pendencia()
    {
        var resultado = PontoApuracaoEngine.Calcular(1, PontoSeg, PontoSeg, ZonaBrasilia, Array.Empty<BatidaPonto>(), _ => JornadaPadrao);

        resultado.DiasFalta.Should().Be(1);
        resultado.TotalAusenciaMinutos.Should().Be(540);
        resultado.TemPendencias.Should().BeFalse();
        resultado.MemoriaPorDia[0].DiaFalta.Should().BeTrue();
    }

    [Fact]
    public void Apuracao_Feriado_No_Dia_Util_Nao_Conta_Falta_Nem_Ausencia()
    {
        var resultado = PontoApuracaoEngine.Calcular(1, PontoSeg, PontoSeg, ZonaBrasilia, Array.Empty<BatidaPonto>(), _ => JornadaPadrao, feriados: new[] { PontoSeg });

        resultado.DiasFalta.Should().Be(0);
        resultado.DiasUteisPrevistos.Should().Be(0);
        resultado.TotalAusenciaMinutos.Should().Be(0);
        resultado.MemoriaPorDia[0].Feriado.Should().BeTrue();
    }

    [Fact]
    public void Apuracao_Ausencia_Justificada_Aprovada_Converte_Falta_Em_Ausencia_Justificada()
    {
        var resultado = PontoApuracaoEngine.Calcular(1, PontoSeg, PontoSeg, ZonaBrasilia, Array.Empty<BatidaPonto>(), _ => JornadaPadrao, ausenciasJustificadas: new[] { PontoSeg });

        resultado.DiasAusenciaJustificada.Should().Be(1);
        resultado.DiasFalta.Should().Be(0);
        resultado.TotalAusenciaMinutos.Should().Be(540); // o tempo segue sendo ausência, sem penalidade
    }

    [Fact]
    public void Apuracao_Intervalo_Incompleto_Marca_Pendencia_E_Nao_Desconta_Suposto_Intervalo()
    {
        var resultado = PontoApuracaoEngine.Calcular(1, PontoSeg, PontoSeg, ZonaBrasilia, new[]
        {
            Batida(1, PontoSeg, new TimeOnly(8, 0), PontoTipo.Entrada),
            Batida(2, PontoSeg, new TimeOnly(12, 0), PontoTipo.IntervaloInicio), // sem IntervaloFim
            Batida(3, PontoSeg, new TimeOnly(17, 0), PontoTipo.Saida)
        }, _ => JornadaPadrao);

        resultado.PendenciasGlobais.Should().ContainSingle().Which.Should().Be("INTERVALO_INCOMPLETO");
        resultado.TotalIntervaloMinutos.Should().Be(0);
        resultado.TotalTrabalhadoMinutos.Should().Be(540);
    }

    [Fact]
    public void Apuracao_Batidas_Incompletas_Marcam_Pendencias_Por_Lado_Sem_Inventar_Minutos()
    {
        // Dias não consecutivos: a saída de quinta não é atribuída à segunda
        // (quarta-feira não tem batidas), isolando as pendências por lado.
        var resultado = PontoApuracaoEngine.Calcular(1, PontoSeg, PontoSeg.AddDays(3), ZonaBrasilia, new[]
        {
            Batida(1, PontoSeg, new TimeOnly(8, 0), PontoTipo.Entrada),           // segunda: só entrada
            Batida(2, PontoSeg.AddDays(3), new TimeOnly(17, 0), PontoTipo.Saida)  // quinta: só saída
        }, _ => JornadaPadrao);

        resultado.PendenciasGlobais.Should().Contain("SAIDA_AUSENTE");
        resultado.PendenciasGlobais.Should().Contain("ENTRADA_AUSENTE");
        resultado.TotalTrabalhadoMinutos.Should().Be(0);
        resultado.DiasFalta.Should().Be(2); // terça e quarta sem nenhuma batida
    }

    [Fact]
    public void Apuracao_Dia_Sem_Escala_Marca_Pendencia_Nao_Count_Falta_E_Trabalho_Fato_Vira_Excedente_Observado()
    {
        var resultado = PontoApuracaoEngine.Calcular(1, PontoSeg, PontoSeg, ZonaBrasilia, new[]
        {
            Batida(1, PontoSeg, new TimeOnly(8, 0), PontoTipo.Entrada),
            Batida(2, PontoSeg, new TimeOnly(9, 0), PontoTipo.Saida)
        }, _ => null); // nenhuma escala cobre o dia

        // RC-EVO-B §3.2: dia sem escala registra o fato observado como excedente observado com pendência
        // nomeada; NÃO vira hora extra remunerável por decisão automática (exige regra aprovada).
        resultado.PendenciasGlobais.Should().Contain("SEM_ESCALA_NO_DIA").And.Contain("TEMPO_SEM_ESCALA_OBSERVADO");
        resultado.DiasSemEscala.Should().Be(1);
        resultado.DiasFalta.Should().Be(0);
        resultado.TotalTrabalhadoMinutos.Should().Be(60);
        resultado.TotalHoraExtraMinutos.Should().Be(0);
        resultado.TotalExcedenteObservadoMinutos.Should().Be(60);
    }

    [Fact]
    public void Apuracao_Horas_Extras_Somente_Acima_Da_Janela_E_Chegada_Antecipada_Nao_E_Atraso()
    {
        var resultado = PontoApuracaoEngine.Calcular(1, PontoSeg, PontoSeg, ZonaBrasilia, new[]
        {
            Batida(1, PontoSeg, new TimeOnly(7, 0), PontoTipo.Entrada),
            Batida(2, PontoSeg, new TimeOnly(19, 0), PontoTipo.Saida)
        }, _ => JornadaPadrao);

        resultado.MemoriaPorDia[0].AtrasoMinutos.Should().Be(0);
        resultado.MemoriaPorDia[0].AntecipacaoMinutos.Should().Be(0);
        resultado.TotalTrabalhadoMinutos.Should().Be(720);
        resultado.TotalHoraExtraMinutos.Should().Be(180);
        resultado.TotalHoraExtra.Should().Be(TimeSpan.FromMinutes(180));
    }

    [Fact]
    public void Apuracao_Fuso_Da_Operacao_Define_O_Dia_Local_Das_Batidas()
    {
        // Mesmos instantes UTC lidos em São Paulo (UTC-3) ou Manaus (UTC-4) caem em dias diferentes.
        var batidas = new[]
        {
            new BatidaPonto(1, new DateTimeOffset(2026, 10, 5, 3, 0, 0, TimeSpan.Zero), PontoTipo.Entrada), // 00:00 SP / dom 23:00 Manaus
            new BatidaPonto(2, new DateTimeOffset(2026, 10, 5, 12, 0, 0, TimeSpan.Zero), PontoTipo.Saida)  // 09:00 SP / 08:00 Manaus
        };

        var emSaoPaulo = PontoApuracaoEngine.Calcular(1, PontoSeg, PontoSeg, ZonaBrasilia, batidas, _ => JornadaPadrao);
        emSaoPaulo.TotalTrabalhadoMinutos.Should().Be(540);
        emSaoPaulo.DiasFalta.Should().Be(0);
        emSaoPaulo.PendenciasGlobais.Should().BeEmpty();

        var emManaus = PontoApuracaoEngine.Calcular(1, PontoSeg, PontoSeg,
            TimeZoneInfo.FindSystemTimeZoneById("America/Manaus"), batidas, _ => JornadaPadrao);
        // RC-EVO-B §3.1: o período é atribuído ao dia LOCAL de abertura (domingo, fora do período);
        // a segunda recebeu o fechamento do turno dominical e por isso não é falta plena.
        emManaus.TotalTrabalhadoMinutos.Should().Be(0);
        emManaus.DiasFalta.Should().Be(0);
        emManaus.MemoriaPorDia[0].DiaFalta.Should().BeFalse();
        emManaus.MemoriaPorDia[0].TemBatidaNoDia.Should().BeTrue();
    }

    // ==== RC-EVO-B §3: engine v2 (RH-APURACAO-2) ==================================

    [Fact]
    public void Apuracao_V2_Dois_Periodos_No_Mesmo_Dia_Somam_Trabalho_Sem_Pendencia()
    {
        var resultado = PontoApuracaoEngine.Calcular(1, PontoSeg, PontoSeg, ZonaBrasilia, new[]
        {
            Batida(1, PontoSeg, new TimeOnly(8, 0), PontoTipo.Entrada),
            Batida(2, PontoSeg, new TimeOnly(12, 0), PontoTipo.Saida),
            Batida(3, PontoSeg, new TimeOnly(13, 0), PontoTipo.Entrada),
            Batida(4, PontoSeg, new TimeOnly(17, 0), PontoTipo.Saida)
        }, _ => JornadaPadrao);

        resultado.PendenciasGlobais.Should().BeEmpty();
        resultado.TotalTrabalhadoMinutos.Should().Be(480);
        resultado.MemoriaPorDia[0].Periodos.Should().HaveCount(2);
    }

    [Fact]
    public void Apuracao_V2_Batida_Duplicada_No_Mesmo_Minuto_Colapsa_Sem_Pendencia()
    {
        var resultado = PontoApuracaoEngine.Calcular(1, PontoSeg, PontoSeg, ZonaBrasilia, new[]
        {
            Batida(1, PontoSeg, new TimeOnly(8, 0), PontoTipo.Entrada),
            Batida(2, PontoSeg, new TimeOnly(8, 0), PontoTipo.Entrada), // mesmo minuto: relance do terminal
            Batida(3, PontoSeg, new TimeOnly(17, 0), PontoTipo.Saida)
        }, _ => JornadaPadrao);

        resultado.PendenciasGlobais.Should().BeEmpty();
        resultado.TotalTrabalhadoMinutos.Should().Be(540);
    }

    [Fact]
    public void Apuracao_V2_Entrada_Consecutiva_Marca_Pendencia_E_Nao_Reabre_Periodo()
    {
        var resultado = PontoApuracaoEngine.Calcular(1, PontoSeg, PontoSeg, ZonaBrasilia, new[]
        {
            Batida(1, PontoSeg, new TimeOnly(8, 0), PontoTipo.Entrada),
            Batida(2, PontoSeg, new TimeOnly(8, 30), PontoTipo.Entrada), // não fecha nem reabre nada
            Batida(3, PontoSeg, new TimeOnly(17, 0), PontoTipo.Saida)
        }, _ => JornadaPadrao);

        resultado.PendenciasGlobais.Should().Contain("ENTRADA_CONSECUTIVA");
        resultado.MemoriaPorDia[0].Periodos.Should().HaveCount(1);
        resultado.MemoriaPorDia[0].EntradaEfetiva.Should().Be(new TimeOnly(8, 0)); // mantém a abertura original
        resultado.TotalTrabalhadoMinutos.Should().Be(540);
    }

    [Fact]
    public void Apuracao_V2_Saida_Além_Da_Janela_Nao_Fecha_Abertura_Antiga_E_Isola_Pendencias()
    {
        var resultado = PontoApuracaoEngine.Calcular(1, PontoSeg, PontoSeg.AddDays(3), ZonaBrasilia, new[]
        {
            Batida(1, PontoSeg, new TimeOnly(8, 0), PontoTipo.Entrada),           // segunda 08:00
            Batida(2, PontoSeg.AddDays(3), new TimeOnly(9, 30), PontoTipo.Saida)  // quinta 09:30 (>24 h de janela)
        }, _ => JornadaPadrao);

        resultado.PendenciasGlobais.Should().Contain("SAIDA_AUSENTE");
        resultado.PendenciasGlobais.Should().Contain("ENTRADA_AUSENTE");
        resultado.PendenciasGlobais.Should().Contain("FORA_DA_JANELA_DE_PAREAMENTO");
        resultado.TotalTrabalhadoMinutos.Should().Be(0);
    }

    [Fact]
    public void Apuracao_V2_Politica_Atraso_Completo_Desconta_O_Atraso_Inteiro_Apos_Tolerancia()
    {
        var batidas = new[]
        {
            Batida(1, PontoSeg, new TimeOnly(8, 5), PontoTipo.Entrada),  // dentro da tolerância de 10
            Batida(2, PontoSeg, new TimeOnly(17, 0), PontoTipo.Saida),
            Batida(3, PontoSeg.AddDays(1), new TimeOnly(8, 25), PontoTipo.Entrada), // 25 > tolerância
            Batida(4, PontoSeg.AddDays(1), new TimeOnly(17, 0), PontoTipo.Saida)
        };

        var sobreExcedente = PontoApuracaoEngine.Calcular(1, PontoSeg, PontoSeg.AddDays(1), ZonaBrasilia, batidas, _ => JornadaPadrao);
        sobreExcedente.TotalAtrasoMinutos.Should().Be(15);

        var atrasoCompleto = PontoApuracaoEngine.Calcular(1, PontoSeg, PontoSeg.AddDays(1), ZonaBrasilia, batidas, _ => JornadaPadrao,
            politicaTolerancia: ToleranciaPolitica.AtrasoCompleto);
        atrasoCompleto.TotalAtrasoMinutos.Should().Be(25); // atraso inteiro, não só o excedente
        atrasoCompleto.MemoriaPorDia[0].AtrasoMinutos.Should().Be(0); // dentro da tolerância continua 0
    }

    [Fact]
    public void Apuracao_V2_Ausencia_Justificada_Fora_Do_Total_Descontavel()
    {
        var justificada = PontoApuracaoEngine.Calcular(1, PontoSeg, PontoSeg, ZonaBrasilia, Array.Empty<BatidaPonto>(), _ => JornadaPadrao,
            ausenciasJustificadas: new[] { PontoSeg });
        justificada.TotalAusenciaMinutos.Should().Be(540);          // total genérico segue informativo
        justificada.TotalAusenciaJustificadaMinutos.Should().Be(540);
        justificada.TotalAusenciaDescontavelMinutos.Should().Be(0);  // folha não desciona abonado

        var falta = PontoApuracaoEngine.Calcular(1, PontoSeg, PontoSeg, ZonaBrasilia, Array.Empty<BatidaPonto>(), _ => JornadaPadrao);
        falta.TotalAusenciaJustificadaMinutos.Should().Be(0);
        falta.TotalAusenciaDescontavelMinutos.Should().Be(540);
    }

    [Fact]
    public void Apuracao_V2_Relacao_Declarada_Carga_X_Programacao_Acima_Gera_Pendencia_Clara()
    {
        // Jornada declara intervalo previsto de 60 min: expectativa líquida 480 min x 5 dias = 40 h > carga 30 h.
        var apertada = new JornadaPontoRegra(3, "Apertada", 30m, new TimeOnly(8, 0), new TimeOnly(17, 0), 0, new[] { 1, 2, 3, 4, 5 }, duracaoPrevistaMinutos: null, intervaloPrevistoMinutos: 60);
        var divergente = PontoApuracaoEngine.Calcular(1, PontoSeg, PontoSeg, ZonaBrasilia, Array.Empty<BatidaPonto>(), _ => apertada);
        divergente.PendenciasGlobais.Should().Contain("PROGRAMACAO_ACIMA_DA_CARGA_SEMANAL");

        // Sem insumo declarado (duração/intervalo) a relação não é julgada: nada é presumido.
        var semDeclaracao = PontoApuracaoEngine.Calcular(1, PontoSeg, PontoSeg, ZonaBrasilia, Array.Empty<BatidaPonto>(), _ => JornadaPadrao);
        semDeclaracao.PendenciasGlobais.Should().NotContain("PROGRAMACAO_ACIMA_DA_CARGA_SEMANAL");
    }

    [Fact]
    public void Apuracao_V2_Entrada_Igual_Saida_No_Mesmo_Instante_E_Tempo_Observado_Zero_Nao_24h()
    {
        var resultado = PontoApuracaoEngine.Calcular(1, PontoSeg, PontoSeg, ZonaBrasilia, new[]
        {
            Batida(1, PontoSeg, new TimeOnly(9, 0), PontoTipo.Entrada),
            Batida(2, PontoSeg, new TimeOnly(9, 0), PontoTipo.Saida)
        }, _ => JornadaPadrao);

        resultado.TotalTrabalhadoMinutos.Should().Be(0);
        resultado.MemoriaPorDia[0].AntecipacaoMinutos.Should().Be(540);
        resultado.MemoriaPorDia[0].AusenciaDescontavelMinutos.Should().Be(540);
    }

    [Fact]
    public void Apuracao_V2_Jornada_Entrada_Igual_Saida_Sem_Duracao_Declarada_Pede_Configuracao()
    {
        var vinteQuatro = new JornadaPontoRegra(4, "24h implícita", 40m, new TimeOnly(8, 0), new TimeOnly(8, 0), 0, new[] { 1, 2, 3, 4, 5 });
        var resultado = PontoApuracaoEngine.Calcular(1, PontoSeg, PontoSeg, ZonaBrasilia, Array.Empty<BatidaPonto>(), _ => vinteQuatro);

        resultado.PendenciasGlobais.Should().Contain("JORNADA_SEM_DURACAO_EXPLICITA");
        resultado.MemoriaPorDia[0].EsperadoMinutos.Should().Be(1440);
    }

    [Fact]
    public void Jornada_Valida_Id_Nome_Carga_Tolerancia_E_Dias_Semana()
    {
        Assert.Throws<ArgumentException>(() => new JornadaPontoRegra(0, "X", 40m, new TimeOnly(8, 0), new TimeOnly(17, 0), 0, new[] { 1 }));
        Assert.Throws<ArgumentException>(() => new JornadaPontoRegra(1, "", 40m, new TimeOnly(8, 0), new TimeOnly(17, 0), 0, new[] { 1 }));
        Assert.Throws<ArgumentException>(() => new JornadaPontoRegra(1, "X", -1m, new TimeOnly(8, 0), new TimeOnly(17, 0), 0, new[] { 1 }));
        Assert.Throws<ArgumentException>(() => new JornadaPontoRegra(1, "X", 40m, new TimeOnly(8, 0), new TimeOnly(17, 0), -1, new[] { 1 }));
        Assert.Throws<ArgumentException>(() => new JornadaPontoRegra(1, "X", 40m, new TimeOnly(8, 0), new TimeOnly(17, 0), 0, new[] { 8 }));
    }

    [Fact]
    public void Apuracao_Rejeita_Periodo_Final_Anterior_Ao_Inicial()
    {
        var ex = Assert.Throws<ArgumentException>(() => PontoApuracaoEngine.Calcular(1, PontoSeg.AddDays(1), PontoSeg, ZonaBrasilia, Array.Empty<BatidaPonto>(), _ => JornadaPadrao));
        ex.Message.Should().Contain("Período final");
    }

    // ==== RC-EVO-RH §5: decisão de justificativas/ajustes (regras puras) ====

    [Fact]
    public void Justificativa_Somente_Estados_Pendentes_Podem_Ser_Decididos()
    {
        PontoTransicoes.ValidarDecisao(null).Should().BeNull();
        PontoTransicoes.ValidarDecisao("RASCUNHO").Should().BeNull();
        PontoTransicoes.ValidarDecisao("PENDENTE").Should().BeNull();
        PontoTransicoes.ValidarDecisao("APROVADA").Should().Be(PontoTransicoes.TransicaoInvalida);
        PontoTransicoes.ValidarDecisao("REPROVADA").Should().Be(PontoTransicoes.TransicaoInvalida);
        PontoTransicoes.ValidarDecisao("CONCLUIDA").Should().Be(PontoTransicoes.TransicaoInvalida);
    }

    [Fact]
    public void Justificativa_Autor_Nao_Decide_Propria_Por_Padrao()
    {
        PontoTransicoes.ValidarAutoprovacao(7, 7).Should().Be(PontoTransicoes.AutoprovacaoBloqueada);
        PontoTransicoes.ValidarAutoprovacao(7, 8).Should().BeNull();
        PontoTransicoes.ValidarAutoprovacao(null, 8).Should().BeNull(); // autor desconhecido: não bloqueia por suposição
    }

    [Fact]
    public void Janela_De_Ajuste_Cobre_Ambos_Instantes_Com_Margem_De_Fuso()
    {
        var antes = new DateTimeOffset(2026, 10, 5, 22, 30, 0, TimeSpan.Zero);
        var depois = new DateTimeOffset(2026, 10, 7, 3, 30, 0, TimeSpan.Zero);

        var (ini, fim) = PontoTransicoes.JanelaAjuste(antes, depois);
        ini.Should().Be(new DateOnly(2026, 10, 4)); // mínimo - 1 dia
        fim.Should().Be(new DateOnly(2026, 10, 8)); // máximo + 1 dia

        var (iniSim, fimSim) = PontoTransicoes.JanelaAjuste(depois, antes); // simétrico
        iniSim.Should().Be(ini);
        fimSim.Should().Be(fim);

        var (ini3, fim3) = PontoTransicoes.JanelaAjuste(antes, antes); // mesmo instante: margem simétrica
        ini3.Should().Be(new DateOnly(2026, 10, 4));
        fim3.Should().Be(new DateOnly(2026, 10, 6));
    }

    [Fact]
    public void Ajuste_Exige_Tipo_De_Batida_Reconhecivel()
    {
        PontoTransicoes.TipoBatidaValido("Entrada").Should().BeTrue();
        PontoTransicoes.TipoBatidaValido("saida").Should().BeTrue();
        PontoTransicoes.TipoBatidaValido("IntervaloFim").Should().BeTrue();
        PontoTransicoes.TipoBatidaValido("Almoço").Should().BeFalse();
        PontoTransicoes.TipoBatidaValido("").Should().BeFalse();
        PontoTransicoes.TipoBatidaValido(null).Should().BeFalse();
    }

    [Fact]
    public void Homologar_Somente_Apartir_De_Apurada()
    {
        PontoTransicoes.ValidarHomologacao("APURADA").Should().BeNull();
        PontoTransicoes.ValidarHomologacao("apurada").Should().BeNull();
        PontoTransicoes.ValidarHomologacao("HOMOLOGADA").Should().Be(PontoTransicoes.TransicaoInvalida);
        PontoTransicoes.ValidarHomologacao("INVALIDADA").Should().Be(PontoTransicoes.TransicaoInvalida);
        PontoTransicoes.ValidarHomologacao("RASCUNHO").Should().Be(PontoTransicoes.TransicaoInvalida);
        PontoTransicoes.ValidarHomologacao("").Should().Be(PontoTransicoes.TransicaoInvalida);
        PontoTransicoes.ValidarHomologacao(null).Should().Be(PontoTransicoes.TransicaoInvalida);
    }

    [Fact]
    public void Reabrir_Somente_Apartir_De_Homologada()
    {
        PontoTransicoes.ValidarReabertura("HOMOLOGADA").Should().BeNull();
        PontoTransicoes.ValidarReabertura("homologada").Should().BeNull();
        PontoTransicoes.ValidarReabertura("APURADA").Should().Be(PontoTransicoes.TransicaoInvalida);
        PontoTransicoes.ValidarReabertura("INVALIDADA").Should().Be(PontoTransicoes.TransicaoInvalida);
        PontoTransicoes.ValidarReabertura(null).Should().Be(PontoTransicoes.TransicaoInvalida);
    }

    [Fact]
    public void Homologar_Exige_Versao_Vigente_Das_Regras()
    {
        PontoTransicoes.VersaoRegrasCompativel("RH-APURACAO-1", "RH-APURACAO-1").Should().BeTrue();
        PontoTransicoes.VersaoRegrasCompativel(" rh-apuracao-1 ", "RH-APURACAO-1").Should().BeTrue();
        PontoTransicoes.VersaoRegrasCompativel("RH-APURACAO-2", "RH-APURACAO-1").Should().BeFalse();
        PontoTransicoes.VersaoRegrasCompativel(null, "RH-APURACAO-1").Should().BeFalse();
        PontoTransicoes.VersaoRegrasCompativel("  ", "RH-APURACAO-1").Should().BeFalse();
    }

    [Fact]
    public void Homologar_Exige_Memoria_Por_Dia_No_Payload()
    {
        var memoriaArray = System.Text.Json.JsonDocument.Parse("[]").RootElement.Clone();
        var memoriaObjeto = System.Text.Json.JsonDocument.Parse("{}").RootElement.Clone();

        PontoTransicoes.PossuiMemoriaPorDia(new Dictionary<string, object?> { ["memoriaPorDia"] = memoriaArray }).Should().BeTrue();
        PontoTransicoes.PossuiMemoriaPorDia(new Dictionary<string, object?> { ["MemoriaPorDia"] = memoriaObjeto }).Should().BeTrue();
        PontoTransicoes.PossuiMemoriaPorDia(new Dictionary<string, object?> { ["status"] = "APURADA" }).Should().BeFalse();
        PontoTransicoes.PossuiMemoriaPorDia(new Dictionary<string, object?> { ["memoriaPorDia"] = null }).Should().BeFalse();
    }

    // ==== RC-EVO-RH §7: regras puras da integração da apuração na folha =====================

    [Fact]
    public void Folha_Somente_Aberta_ou_Calculada_Aceita_Integracao()
    {
        FolhaRegras.ValidarStatusFolha("ABERTA").Should().BeNull();
        FolhaRegras.ValidarStatusFolha("calculada").Should().BeNull();
        FolhaRegras.ValidarStatusFolha(" CALCULADA ").Should().BeNull();
        FolhaRegras.ValidarStatusFolha("FECHADA").Should().StartWith(FolhaRegras.FalhaFolhaFechada);
        FolhaRegras.ValidarStatusFolha("cancelada").Should().StartWith(FolhaRegras.FalhaFolhaCancelada);
        FolhaRegras.ValidarStatusFolha("PROCESSANDO").Should().StartWith(FolhaRegras.FalhaFolhaStatusInvalido);
        FolhaRegras.ValidarStatusFolha("").Should().StartWith(FolhaRegras.FalhaFolhaStatusInvalido);
        FolhaRegras.ValidarStatusFolha(null).Should().StartWith(FolhaRegras.FalhaFolhaStatusInvalido);
    }

    [Fact]
    public void StatusDeIntegracao_Somente_Os_Without_Efeito_Materializado_Sao_Reutilizaveis()
    {
        FolhaRegras.StatusIntegracaoReutilizavel("PENDENTE").Should().BeTrue();
        FolhaRegras.StatusIntegracaoReutilizavel("FALHA").Should().BeTrue();
        FolhaRegras.StatusIntegracaoReutilizavel("CANCELADA").Should().BeTrue();
        FolhaRegras.StatusIntegracaoReutilizavel("processada").Should().BeTrue();
        FolhaRegras.StatusIntegracaoReutilizavel("PROCESSANDO").Should().BeFalse();
        FolhaRegras.StatusIntegracaoReutilizavel("RASCUNHO").Should().BeFalse();
        FolhaRegras.StatusIntegracaoReutilizavel(null).Should().BeFalse();
    }

    [Fact]
    public void Critica_Bloqueia_Somente_Com_Pendencias_E_Parametro_Habilitado()
    {
        FolhaRegras.CriticaBloqueia(true, false, new[] { "PENDENCIA" }).Should().BeTrue();
        FolhaRegras.CriticaBloqueia(true, true, new[] { "PENDENCIA" }).Should().BeFalse();
        FolhaRegras.CriticaBloqueia(false, false, new[] { "PENDENCIA" }).Should().BeFalse();
        FolhaRegras.CriticaBloqueia(true, false, Array.Empty<string>()).Should().BeFalse();
        FolhaRegras.CriticaBloqueia(true, true, Array.Empty<string>()).Should().BeFalse();
        FolhaRegras.CriticaBloqueia(true, false, null).Should().BeFalse();
    }

    [Fact]
    public void Rubricas_Ausentes_ou_Vazias_Geram_Falha_Nomeada()
    {
        var (semJson, falhaNula) = FolhaRegras.InterpretarRubricasPonto(null);
        falhaNula.Should().StartWith(FolhaRegras.RubricasAusentes);
        semJson.Should().BeEmpty();

        var (espaco, falhaEspaco) = FolhaRegras.InterpretarRubricasPonto(" ");
        falhaEspaco.Should().StartWith(FolhaRegras.RubricasAusentes);
        espaco.Should().BeEmpty();

        var (arrayVazio, falhaArray) = FolhaRegras.InterpretarRubricasPonto("[]");
        falhaArray.Should().StartWith(FolhaRegras.RubricasAusentes);
        arrayVazio.Should().BeEmpty();
    }

    [Fact]
    public void Rubricas_Validas_Normalizam_Tipo_Base_E_Aceitam_Taxa_String()
    {
        const string json = "[{\"codigo\":\"VT-EXT\",\"nome\":\"Viagem\",\"tipo\":\"provento\",\"base\":\"minutos_trabalhados\",\"taxa\":15},{\"codigo\":\"DES-FALTA\",\"nome\":\"Falta\",\"tipo\":\"DESCONTO\",\"base\":\"DIAS_FALTA\",\"taxa\":\"2.5\"}]";
        var (rubricas, falha) = FolhaRegras.InterpretarRubricasPonto(json);
        falha.Should().BeNull();
        rubricas.Should().HaveCount(2);
        rubricas[0].Codigo.Should().Be("VT-EXT");
        rubricas[0].Tipo.Should().Be(FolhaRegras.RubricaProvento);
        rubricas[0].Base.Should().Be(FolhaRegras.BaseMinutosTrabalhados);
        rubricas[0].Taxa.Should().Be(15m);
        rubricas[0].EmMinutos.Should().BeTrue();
        rubricas[1].Tipo.Should().Be(FolhaRegras.RubricaDesconto);
        rubricas[1].Base.Should().Be(FolhaRegras.BaseDiasFalta);
        rubricas[1].Taxa.Should().Be(2.5m);
        rubricas[1].EmMinutos.Should().BeFalse();
    }

    [Fact]
    public void Rubricas_Invalidas_Apontam_O_Item_Problema()
    {
        void DeveriaFalhar(string json)
        {
            var (rubricas, falha) = FolhaRegras.InterpretarRubricasPonto(json);
            falha.Should().StartWith(FolhaRegras.RubricasInvalidas);
            falha.Should().Contain("[0]");
            rubricas.Should().BeEmpty();
        }

        DeveriaFalhar("[\"NÃO-OBJETO\"]"); // item não é objeto
        DeveriaFalhar("[{\"codigo\":\"A\",\"tipo\":\"PROVENTO\",\"base\":\"MINUTOS_TRABALHADOS\",\"taxa\":1}]"); // nome ausente
        DeveriaFalhar("[{\"codigo\":\"A\",\"nome\":\"N\",\"tipo\":\"XPTO\",\"base\":\"MINUTOS_TRABALHADOS\",\"taxa\":1}]"); // tipo inválido
        DeveriaFalhar("[{\"codigo\":\"A\",\"nome\":\"N\",\"tipo\":\"PROVENTO\",\"base\":\"SEMANAS\",\"taxa\":1}]"); // base inválida
        DeveriaFalhar("[{\"codigo\":\"A\",\"nome\":\"N\",\"tipo\":\"PROVENTO\",\"base\":\"MINUTOS_TRABALHADOS\",\"taxa\":-2}]"); // taxa negativa
        DeveriaFalhar("[{\"codigo\":\"A\",\"nome\":\"N\",\"tipo\":\"PROVENTO\",\"base\":\"MINUTOS_TRABALHADOS\"}]"); // taxa ausente

        var (naoArray, falhaNaoArray) = FolhaRegras.InterpretarRubricasPonto("\"123\"");
        falhaNaoArray.Should().StartWith(FolhaRegras.RubricasInvalidas);
        naoArray.Should().BeEmpty();

        var (malformado, falhaMalformado) = FolhaRegras.InterpretarRubricasPonto("{");
        falhaMalformado.Should().StartWith(FolhaRegras.RubricasInvalidas);
        malformado.Should().BeEmpty();

        var (duplicado, falhaDuplicado) = FolhaRegras.InterpretarRubricasPonto("[{\"codigo\":\"A\",\"nome\":\"N\",\"tipo\":\"PROVENTO\",\"base\":\"MINUTOS_TRABALHADOS\",\"taxa\":1},{\"codigo\":\"a\",\"nome\":\"M\",\"tipo\":\"DESCONTO\",\"base\":\"DIAS_FALTA\",\"taxa\":2}]");
        falhaDuplicado.Should().StartWith(FolhaRegras.RubricasInvalidas);
        falhaDuplicado.Should().Contain("[1]");
        duplicado.Should().BeEmpty();
    }

    [Fact]
    public void Lancamento_Multiplica_Taxa_Pela_Base_Minutos_ou_Dias()
    {
        var porMinuto = new FolhaRegras.RubricaPonto("A", "A", FolhaRegras.RubricaProvento, FolhaRegras.BaseMinutosTrabalhados, 15m);
        FolhaRegras.QuantidadeBase(porMinuto, 90, 30, 0).Should().Be(90);
        FolhaRegras.ValorLancamento(porMinuto, 90).Should().Be(22.50m); // 15 x 90 min / 60
        FolhaRegras.ValorLancamento(porMinuto, 0).Should().Be(0m);

        var porDia = new FolhaRegras.RubricaPonto("F", "F", FolhaRegras.RubricaDesconto, FolhaRegras.BaseDiasFalta, 100m);
        FolhaRegras.QuantidadeBase(porDia, 0, 0, 2).Should().Be(2);
        FolhaRegras.ValorLancamento(porDia, 2).Should().Be(200m);

        var porIntervalo = new FolhaRegras.RubricaPonto("I", "I", FolhaRegras.RubricaProvento, FolhaRegras.BaseMinutosIntervalo, 1.5m);
        FolhaRegras.QuantidadeBase(porIntervalo, 10, 25, 0).Should().Be(25);
        FolhaRegras.ValorLancamento(porIntervalo, 5).Should().Be(0.13m); // 1.5 x 5 / 60 = 0.125 -> 0.13 (AwayFromZero)
    }

    // ==== RC-EVO-RH §8: portal com escopo próprio ==============================================

    [Fact]
    public void Portal_ServidorId_Do_Payload_E_FailClosed()
    {
        PortalRegras.LerServidorId("42").Should().Be(42L);
        PortalRegras.LerServidorId(" 7 ").Should().Be(7L);
        PortalRegras.LerServidorId(null).Should().BeNull();
        PortalRegras.LerServidorId("").Should().BeNull();
        PortalRegras.LerServidorId("abc").Should().BeNull();
        PortalRegras.LerServidorId("1.5").Should().BeNull();

        PortalRegras.PertenceAoServidor("42", 42).Should().BeTrue();
        PortalRegras.PertenceAoServidor("42", 43).Should().BeFalse();
        PortalRegras.PertenceAoServidor(null, 42).Should().BeFalse();
    }

    [Fact]
    public void Portal_Totais_Sao_Recalculados_Dos_Lancamentos_Sem_Sinal_Inventado()
    {
        var lancamentos = new (string, decimal)[]
        {
            ("PROVENTO", -100m),   // valor gravado negativo vira absoluto; sinal vem do tipo
            ("desconto", 30.5m),
            ("OUTRO_TIPO", 50m),   // tipo não reconhecido contribui zero
            (null!, 99m),          // tipo ausente contribui zero
        };

        var (proventos, descontos, liquido) = PortalRegras.TotaisPorTipo(lancamentos);
        proventos.Should().Be(100m);
        descontos.Should().Be(30.5m);
        liquido.Should().Be(69.5m);
    }

    [Fact]
    public void Portal_Pendencias_Sao_Classificadas_Pelo_Status_De_Cada_Tipo()
    {
        // Apuração: só APURADA é pendência; HOMOLOGADA/RASCUNHO fecham ou não se enquadram.
        PortalRegras.EhPendencia("APURACAO", "APURADA").Should().BeTrue();
        PortalRegras.EhPendencia("APURACAO", "apurada").Should().BeTrue(); // case-insensitive
        PortalRegras.EhPendencia("APURACAO", "HOMOLOGADA").Should().BeFalse();
        PortalRegras.EhPendencia("APURACAO", null).Should().BeFalse();

        // Justificativa: estados em análise são pendência; decisão (APROVADA/REPROVADA) encerra.
        PortalRegras.EhPendencia("JUSTIFICATIVA", "PENDENTE").Should().BeTrue();
        PortalRegras.EhPendencia("JUSTIFICATIVA", "").Should().BeTrue();
        PortalRegras.EhPendencia("JUSTIFICATIVA", "APROVADA").Should().BeFalse();
        PortalRegras.EhPendencia("JUSTIFICATIVA", "REPROVADA").Should().BeFalse();

        // Integração: PROCESSADA/CANCELADA encerram; o restante é pendência.
        PortalRegras.EhPendencia("INTEGRACAO_FOLHA", "PROCESSADA").Should().BeFalse();
        PortalRegras.EhPendencia("INTEGRACAO_FOLHA", "CANCELADA").Should().BeFalse();
        PortalRegras.EhPendencia("INTEGRACAO_FOLHA", "PENDENTE").Should().BeTrue();
        PortalRegras.EhPendencia("INTEGRACAO_FOLHA", "FALHA").Should().BeTrue();
        PortalRegras.EhPendencia("INTEGRACAO_FOLHA", null).Should().BeTrue();

        // Tipo desconhecido nunca é pendência.
        PortalRegras.EhPendencia("DESPACHO", "APURADA").Should().BeFalse();
    }

    [Fact]
    public void Portal_MemoriaPorDia_E_Competencia_Leem_Ausencias_Como_Null()
    {
        var comMemoria = JsonDocument.Parse("""{"memoriaPorDia":[{"dia":"2026-09-01"}]}""");
        PortalRegras.MemoriaPorDia(comMemoria.RootElement).Should().NotBeNull();

        var camel = JsonDocument.Parse("""{"memoriaPorDia":{"a":1}}""");
        PortalRegras.MemoriaPorDia(camel.RootElement).Should().NotBeNull();

        var pascal = JsonDocument.Parse("""{"MemoriaPorDia":[{"dia":"x"}]}""");
        PortalRegras.MemoriaPorDia(pascal.RootElement).Should().NotBeNull(); // case-insensitive

        var ausente = JsonDocument.Parse("""{"resumo":{"total":1}}""");
        PortalRegras.MemoriaPorDia(ausente.RootElement).Should().BeNull();

        var invalido = JsonDocument.Parse("""{"memoriaPorDia":"texto"}""");
        PortalRegras.MemoriaPorDia(invalido.RootElement).Should().BeNull(); // nem array nem objeto → null explícita

        var escopo = JsonDocument.Parse("[1,2]");
        PortalRegras.MemoriaPorDia(escopo.RootElement).Should().BeNull();

        PortalRegras.Competencia(2026, 9, new DateOnly(2026, 8, 31)).Should().Be("2026-09");
        PortalRegras.Competencia(null, null, new DateOnly(2026, 8, 31)).Should().Be("2026-08");
        PortalRegras.Competencia(0, 9, null).Should().BeNull();   // ano inválido e sem período → não deriva
        PortalRegras.Competencia(2026, 0, null).Should().BeNull(); // mês inválido
        PortalRegras.Competencia(null, null, null).Should().BeNull();
    }

    // ==== RC-EVO-RH §9: regras puras da relação Folha de ponto → Financeiro ===========

    private static string PayloadJson(string itens) =>
        """{"integracaoId":77,"apuracaoId":88,"folhaId":9,"servidorId":3,"versaoRegras":"RH-PONTO-v3","periodoInicio":"2026-08-28","periodoFim":"2026-09-27","competenciaAno":2026,"competenciaMes":9,"entidadeId":1,"exercicioId":1,"totalProventos":13.00,"totalDescontos":4.00,"usuarioId":102,"dataEmissao":"2026-10-05","itens":[__ITENS__]}""".Replace("__ITENS__", itens);

    [Fact]
    public void S9_Parse_Aceita_CamelCase_E_Legado_PascalCase_Sem_Inventar_Valores()
    {
        var payload = FolhaPontoFinanceiraRegras.Parse(PayloadJson("""{"codigo":"PONT-HOR","nome":"Horas trabalhadas (ponto)","tipo":"PROVENTO","valor":10.00}"""));
        payload.IntegracaoId.Should().Be(77);
        payload.ApuracaoId.Should().Be(88);
        payload.PeriodoInicio.Should().Be(new DateOnly(2026, 8, 28));
        payload.PeriodoFim.Should().Be(new DateOnly(2026, 9, 27));
        payload.CompetenciaMes.Should().Be(9);
        payload.UsuarioId.Should().Be(102);
        payload.Itens.Should().ContainSingle().Which.Valor.Should().Be(10.00m);

        // Legacy PascalCase continua legível (produções anteriores) e ausência de opcional → null explícita.
        var pascal = """{"IntegracaoId":77,"ApuracaoId":88,"FolhaId":9,"ServidorId":3,"VersaoRegras":"RH-PONTO-v3","PeriodoInicio":"2026-08-28","PeriodoFim":"2026-09-27","CompetenciaAno":2026,"CompetenciaMes":9,"EntidadeId":1,"ExercicioId":1,"TotalProventos":13.00,"TotalDescontos":4.00,"DataEmissao":"2026-10-05","Itens":[{"Codigo":"PONT-INT","Nome":"Intervalo fracionado (ponto)","Tipo":"PROVENTO","Valor":3.00}]}""";
        var legado = FolhaPontoFinanceiraRegras.Parse(pascal);
        legado.IntegracaoId.Should().Be(77);
        legado.Itens.Should().ContainSingle().Which.Valor.Should().Be(3.00m);
        legado.UsuarioId.Should().BeNull();
    }

    [Fact]
    public void S9_Parse_Emite_Falha_Nomeada_Citando_O_Campo_Ausente_Ou_Invalido()
    {
        Assert.Throws<InvalidOperationException>(() => FolhaPontoFinanceiraRegras.Parse(null))
            .Message.Should().Contain(FolhaPontoFinanceiraRegras.FalhaPayloadInvalido);

        Assert.Throws<InvalidOperationException>(() => FolhaPontoFinanceiraRegras.Parse("{"))
            .Message.Should().Contain("não é um JSON válido");

        Assert.Throws<InvalidOperationException>(() => FolhaPontoFinanceiraRegras.Parse("[1]"))
            .Message.Should().Contain("esperado objeto JSON");

        var baseItem = """{"codigo":"PONT-HOR","nome":"x","tipo":"PROVENTO","valor":1}""";
        var semIntegracao = PayloadJson(baseItem).Replace("\"integracaoId\":77,", "");
        Assert.Throws<InvalidOperationException>(() => FolhaPontoFinanceiraRegras.Parse(semIntegracao))
            .Message.Should().Contain(FolhaPontoFinanceiraRegras.FalhaPayloadInvalido).And.Contain("IntegracaoId");

        var mesInvalido = PayloadJson(baseItem).Replace("\"competenciaMes\":9", "\"competenciaMes\":13");
        Assert.Throws<InvalidOperationException>(() => FolhaPontoFinanceiraRegras.Parse(mesInvalido))
            .Message.Should().Contain("CompetenciaMes");
    }

    [Fact]
    public void S9_ConstruirEmpenho_So_Empenha_Proventos_Positivos_E_Guarda_Memoria_Nas_Observacoes()
    {
        const string itens = """{"codigo":"PONT-HOR","nome":"Horas trabalhadas (ponto)","tipo":"PROVENTO","valor":10.00},{"codigo":"PONT-INT","nome":"Intervalo fracionado (ponto)","tipo":"PROVENTO","valor":3.00},{"codigo":"PONT-FALTA","nome":"Dia de falta (ponto)","tipo":"DESCONTO","valor":4.00},{"codigo":"PONT-ZERO","nome":"Provento zerado","tipo":"PROVENTO","valor":0.00}""";
        var payload = FolhaPontoFinanceiraRegras.Parse(PayloadJson(itens));

        var plano = FolhaPontoFinanceiraRegras.ConstruirEmpenho(payload, orcamentoDespesaId: 1, fornecedorPessoaId: 3);

        plano.OrcamentoDespesaId.Should().Be(1);
        plano.FornecedorPessoaId.Should().Be(3);
        plano.ValorTotal.Should().Be(13.00m);                       // desconto e provento zerado ficam fora do documento
        plano.Itens.Should().HaveCount(2);                          // apenas PONT-HOR + PONT-INT
        plano.Itens.Should().AllSatisfy(i => i.Quantidade.Should().Be(1m));
        plano.Itens[0].Descricao.Should().Be("Ponto: PONT-HOR Horas trabalhadas (ponto) (9/2026)");
        plano.Historico.Should().Contain("9/2026").And.Contain("servidor 3").And.Contain("RH-PONTO-v3");
        plano.Observacoes.Should().Contain("proventos")
            .And.Contain("descontos")
            .And.Contain("Empenho sem liquidação nem pagamento automático.");
    }

    [Fact]
    public void S9_ConstruirEmpenho_Sem_Provento_Positivo_Falha_Nomeada_Sem_Valor_Ficticio()
    {
        const string baseItem = """{"codigo":"PONT-HOR","nome":"x","tipo":"PROVENTO","valor":1.00}""";

        var soDesconto = FolhaPontoFinanceiraRegras.Parse(PayloadJson("""{"codigo":"PONT-FALTA","nome":"Dia de falta (ponto)","tipo":"DESCONTO","valor":4.00}"""));
        Assert.Throws<InvalidOperationException>(() => FolhaPontoFinanceiraRegras.ConstruirEmpenho(soDesconto, 1, 3))
            .Message.Should().Contain(FolhaPontoFinanceiraRegras.FalhaSemProventos);

        var soZero = FolhaPontoFinanceiraRegras.Parse(PayloadJson("""{"codigo":"PONT-ZERO","nome":"Provento zerado","tipo":"PROVENTO","valor":0.00}"""));
        Assert.Throws<InvalidOperationException>(() => FolhaPontoFinanceiraRegras.ConstruirEmpenho(soZero, 1, 3))
            .Message.Should().Contain(FolhaPontoFinanceiraRegras.FalhaSemProventos);

        // Regra insuficiente falha antes de qualquer documento.
        var proventoValido = FolhaPontoFinanceiraRegras.Parse(PayloadJson(baseItem));
        Assert.Throws<InvalidOperationException>(() => FolhaPontoFinanceiraRegras.ConstruirEmpenho(proventoValido, 0, 3))
            .Message.Should().Contain(FolhaPontoFinanceiraRegras.FalhaRegrasInsuficientes);
    }

    [Fact]
    public void S9_ValidarRegras_Sinaliza_Cada_Parametro_Ausente_Nomeadamente()
    {
        FolhaPontoFinanceiraRegras.ValidarRegras(1, 3).Should().BeNull();

        var semOrcamento = FolhaPontoFinanceiraRegras.ValidarRegras(0, 3)!;
        semOrcamento.Should().Contain(FolhaPontoFinanceiraRegras.FalhaRegrasInsuficientes)
            .And.Contain(FolhaPontoFinanceiraRegras.ParametroOrcamento)
            .And.NotContain(FolhaPontoFinanceiraRegras.ParametroFornecedor);

        var semFornecedor = FolhaPontoFinanceiraRegras.ValidarRegras(5, 0)!;
        semFornecedor.Should().Contain(FolhaPontoFinanceiraRegras.ParametroFornecedor)
            .And.NotContain(FolhaPontoFinanceiraRegras.ParametroOrcamento);

        var nenhum = FolhaPontoFinanceiraRegras.ValidarRegras(0, 0)!;
        nenhum.Should().Contain(FolhaPontoFinanceiraRegras.ParametroOrcamento)
            .And.Contain(FolhaPontoFinanceiraRegras.ParametroFornecedor);
    }

    [Fact]
    public void S9_InterpretarParametroLong_E_FailClosed_Para_Vazio_Null_Ou_Invalido()
    {
        FolhaPontoFinanceiraRegras.InterpretarParametroLong(null).Should().Be(0);
        FolhaPontoFinanceiraRegras.InterpretarParametroLong("").Should().Be(0);
        FolhaPontoFinanceiraRegras.InterpretarParametroLong("   ").Should().Be(0);
        FolhaPontoFinanceiraRegras.InterpretarParametroLong("null").Should().Be(0); // valor_padrao neutro do catálogo
        FolhaPontoFinanceiraRegras.InterpretarParametroLong("12").Should().Be(12);
        FolhaPontoFinanceiraRegras.InterpretarParametroLong("-5").Should().Be(-5);
        FolhaPontoFinanceiraRegras.InterpretarParametroLong("\"12\"").Should().Be(12); // JSON string numérico
        FolhaPontoFinanceiraRegras.InterpretarParametroLong("\"abc\"").Should().Be(0);
        FolhaPontoFinanceiraRegras.InterpretarParametroLong("1E2").Should().Be(0);   // notação científica não é INTEGER simples → falha fechada (0)
        FolhaPontoFinanceiraRegras.InterpretarParametroLong("7.5").Should().Be(0);   // decimal não é INTEGER válido → 0
        FolhaPontoFinanceiraRegras.InterpretarParametroLong("{\"id\":9}").Should().Be(0);
        FolhaPontoFinanceiraRegras.InterpretarParametroLong("[9]").Should().Be(0);
        FolhaPontoFinanceiraRegras.InterpretarParametroLong("true").Should().Be(0);
    }

    [Fact]
    public void S9_Chave_Integracao_E_Money_Sao_Deterministicos()
    {
        FolhaPontoFinanceiraRegras.ChaveIntegracao(5, 77).Should().Be("rh.folha-ponto-5-77");
        FolhaPontoFinanceiraRegras.ChaveIntegracao(1, 77).Should().Be("rh.folha-ponto-1-77");

        FolhaPontoFinanceiraRegras.Money(10.345m).Should().Be(10.35m);
        FolhaPontoFinanceiraRegras.Money(-10.345m).Should().Be(-10.35m);
        FolhaPontoFinanceiraRegras.Money(10.344m).Should().Be(10.34m);
    }

    // ==== RC-EVO-B §5: emissão congelada, payload assinado e tipo parametrizado =======

    [Fact]
    public void S5_Parse_Exige_DataEmissao_Congelada_Na_Publicacao_Sem_Inventar_Data()
    {
        const string item = """{"codigo":"PONT-HOR","nome":"x","tipo":"PROVENTO","valor":10.00}""";

        // RC-EVO-B §5: documento não pode nascer com a data do dia do retry — ausência é falha nomeada.
        var semEmissao = PayloadJson(item).Replace("\"dataEmissao\":\"2026-10-05\",", "");
        Assert.Throws<InvalidOperationException>(() => FolhaPontoFinanceiraRegras.Parse(semEmissao))
            .Message.Should().Contain(FolhaPontoFinanceiraRegras.FalhaPayloadInvalido)
            .And.Contain("DataEmissao");

        var emitido = PayloadJson(item).Replace("\"dataEmissao\":\"2026-10-05\"", "\"dataEmissao\":\"31/12/2026\"");
        Assert.Throws<InvalidOperationException>(() => FolhaPontoFinanceiraRegras.Parse(emitido))
            .Message.Should().Contain(FolhaPontoFinanceiraRegras.FalhaPayloadInvalido);

        FolhaPontoFinanceiraRegras.Parse(PayloadJson(item)).DataEmissao.Should().Be(new DateOnly(2026, 10, 5));
    }

    [Fact]
    public void S5_Hash_Do_Payload_E_Deterministico_E_Verificacao_Pega_Alteracao_De_Valor()
    {
        const string item = """{"codigo":"PONT-HOR","nome":"Horas trabalhadas (ponto)","tipo":"PROVENTO","valor":10.00}""";

        // Producer assina sem o campo hash; consumer recompara a partir do parse (ordem de JSON irrelevante).
        var bruto = FolhaPontoFinanceiraRegras.Parse(PayloadJson(item));
        var hash = FolhaPontoFinanceiraRegras.CalcularHash(bruto);
        hash.Should().MatchRegex("^[0-9a-f]{64}$");

        var assinadoJson = PayloadJson(item).Replace("\"itens\":[", $"\"payloadHash\":\"{hash}\",\"itens\":[");
        var assinado = FolhaPontoFinanceiraRegras.Parse(assinadoJson);
        FolhaPontoFinanceiraRegras.VerificarIntegridade(assinado); // não lança

        // Reordenação de propriedades não muda o hash (forma canônica, não texto cru).
        var reordenado = FolhaPontoFinanceiraRegras.Parse(assinadoJson.Replace("\"servidorId\":3,", "").Replace("\"itens\":[", "\"servidorId\":3,\"itens\":["));
        FolhaPontoFinanceiraRegras.VerificarIntegridade(reordenado);

        // Dinheiro alterado entre publicar e consumir → checksum divergente nomeado.
        var adulterado = FolhaPontoFinanceiraRegras.Parse(assinadoJson.Replace("\"valor\":10.00", "\"valor\":99.00"));
        Assert.Throws<InvalidOperationException>(() => FolhaPontoFinanceiraRegras.VerificarIntegridade(adulterado))
            .Message.Should().Contain(FolhaPontoFinanceiraRegras.FalhaChecksumDivergente);
    }

    [Fact]
    public void S5_Payload_Sem_Assinatura_Falha_Nomeada_Antes_De_Qualquer_Efeito()
    {
        const string item = """{"codigo":"PONT-HOR","nome":"x","tipo":"PROVENTO","valor":10.00}""";
        var semHash = FolhaPontoFinanceiraRegras.Parse(PayloadJson(item));
        Assert.Throws<InvalidOperationException>(() => FolhaPontoFinanceiraRegras.VerificarIntegridade(semHash))
            .Message.Should().Contain(FolhaPontoFinanceiraRegras.FalhaHashAusente);
    }

    [Fact]
    public void S5_TipoEmpenho_Vem_Do_Parametro_Com_Padrao_ORDINARIO_Documentado()
    {
        FolhaPontoFinanceiraRegras.ParametroTipoEmpenho.Should().Be("TIPO_EMPENHO_INTEGRACAO_PONTO");

        FolhaPontoFinanceiraRegras.InterpretarTipoEmpenho(null).Should().Be("ORDINARIO");
        FolhaPontoFinanceiraRegras.InterpretarTipoEmpenho("").Should().Be("ORDINARIO");
        FolhaPontoFinanceiraRegras.InterpretarTipoEmpenho("  ").Should().Be("ORDINARIO");
        FolhaPontoFinanceiraRegras.InterpretarTipoEmpenho("\"\"").Should().Be("ORDINARIO");
        FolhaPontoFinanceiraRegras.InterpretarTipoEmpenho("global").Should().Be("GLOBAL");
        FolhaPontoFinanceiraRegras.InterpretarTipoEmpenho("\"patrimonial\"").Should().Be("PATRIMONIAL");
        FolhaPontoFinanceiraRegras.InterpretarTipoEmpenho("\"  estimativo  \"").Should().Be("ESTIMATIVO");
    }
}
