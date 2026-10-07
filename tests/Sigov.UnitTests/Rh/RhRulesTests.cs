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
        resultado.VersaoRegras.Should().Be("RH-APURACAO-1");
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
    public void Apuracao_Virada_De_Dia_Nao_Attribui_Quando_O_Dia_Anterior_Ja_Tem_Saida()
    {
        var noturno = new JornadaPontoRegra(2, "Noturno", 44m, new TimeOnly(18, 0), new TimeOnly(6, 0), 0, new[] { 1, 2, 3, 4, 5 });
        // Quarta tem entrada+saída própria (fechada); a saída de quinta não pode "pular" para quarta.
        var resultado = PontoApuracaoEngine.Calcular(1, PontoSeg.AddDays(2), PontoSeg.AddDays(3), ZonaBrasilia, new[]
        {
            Batida(1, PontoSeg.AddDays(2), new TimeOnly(18, 0), PontoTipo.Entrada),
            Batida(2, PontoSeg.AddDays(2), new TimeOnly(6, 0), PontoTipo.Saida),
            Batida(3, PontoSeg.AddDays(3), new TimeOnly(6, 0), PontoTipo.Saida)
        }, _ => noturno);

        resultado.MemoriaPorDia[0].Pendencias.Should().BeEmpty(); // quarta completa, intocada
        resultado.MemoriaPorDia[1].Pendencias.Should().Contain("ENTRADA_AUSENTE"); // saída solta permanece na quinta
        resultado.TotalTrabalhadoMinutos.Should().Be(720); // somente o turno de quarta completo
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
    public void Apuracao_Dia_Sem_Escala_Marca_Pendencia_Nao_Count_Falta_E_Trabalho_Fato_Vira_Extra()
    {
        var resultado = PontoApuracaoEngine.Calcular(1, PontoSeg, PontoSeg, ZonaBrasilia, new[]
        {
            Batida(1, PontoSeg, new TimeOnly(8, 0), PontoTipo.Entrada),
            Batida(2, PontoSeg, new TimeOnly(9, 0), PontoTipo.Saida)
        }, _ => null); // nenhuma escala cobre o dia

        resultado.PendenciasGlobais.Should().ContainSingle().Which.Should().Be("SEM_ESCALA_NO_DIA");
        resultado.DiasSemEscala.Should().Be(1);
        resultado.DiasFalta.Should().Be(0);
        resultado.TotalTrabalhadoMinutos.Should().Be(60);
        resultado.TotalHoraExtraMinutos.Should().Be(60);
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
        emManaus.TotalTrabalhadoMinutos.Should().Be(0);
        emManaus.DiasFalta.Should().Be(1); // a saída migrou com a virada de dia para domingo (fora do período)
        emManaus.MemoriaPorDia[0].DiaFalta.Should().BeTrue();
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
}
