namespace Sigov.Domain.Rh;

// ============================================================================
// RC-EVO-RH §4 · RC-EVO-B §3 - Engine de apuração real de ponto (v2).
// C# puro, sem persistência: recebe a regra da jornada, as batidas, os feriados e
// as ausências justificadas aprovadas; devolve o resultado agregado + memória por
// dia, sempre em minutos/TimeSpan (nunca "1,30 horas"). A versão das regras é
// explícita (VersaoRegras) e deve ser gravada junto de cada apuração calculada;
// resultados de versões anteriores NÃO são reclassificados silenciosamente (a
// homologação recusa versão divergente e exige reabertura + reapuração).
// Nenhuma legislação fica embutida: apenas aritmética entre batidas registradas e
// a jornada configurada. Tolerância, feriados e justificativas são entradas
// fornecidas pelo chamador (autoridade do banco), parametrizadas por jornada.
// Convenções da v2 (RC-EVO-B §3.1/§3.2/§3.3):
//  - diasSemana segue ISO-8601 (1 = segunda .. 7 = domingo);
//  - segundos das batidas são truncados para minutos (política explícita:
//    truncamento do instante e das durações para baixo, nunca arredondamento);
//  - PAREAMENTO CRONOLÓGICO POR INSTANTE (substitui first-in/last-out): as batidas
//    do servidor são ordenadas pelo instante absoluto e interpretadas como uma
//    sequência: Entrada abre período, Saida fecha o período aberto, IntervaloInicio/
//    IntervaloFim fecham dentro do período aberto. O período é atribuído ao dia
//    LOCAL de abertura (turno noturno atravessa a meia-noite naturalmente, sem
//    "pular" saída para o dia anterior apenas por falta de entrada no dia atual);
//  - sequência inválida nunca é completada em silêncio: Entrada consecutiva marca
//    ENTRADA_CONSECUTIVA (a segunda não reabre), Saída sem período aberto marca
//    ENTRADA_AUSENTE no dia dela, Entrada sem fechamento marca SAIDA_AUSENTE no dia
//    de abertura, intervalo sem par marca INTERVALO_INCOMPLETO e não desconta nada;
//  - batidas duplicadas (mesmo tipo no mesmo minuto) são o mesmo fato físico do
//    terminal e colapsam; o pareamento respeita uma janela máxima (padrão 24 h):
//    Saida além da janela não fecha a Entrada antiga, que fica com SAIDA_AUSENTE;
//  - batida com tipo Ajuste é marcador de registro ajustado (o ajuste materializa
//    nova dataHora/tipo) e não participa do pareamento;
//  - entrada igual à saída no MESMO instante significa tempo observado 0 — nunca
//    24 h; 24 h só existe como janela quando a jornada declara duração prevista
//    explícita (DuracaoPrevistaMinutos) ou quando a janela horário a isso equivale,
//    situação marcada por JORNADA_SEM_DURACAO_EXPLICITA para revisão;
//  - dia sem escala registra o tempo OBSERVADO como excedente observado com
//    pendência TEMPO_SEM_ESCALA_OBSERVADO e NÃO vira hora extra remunerável por
//    decisão automática (exige regra aprovada no fluxo de exceções);
//  - ausência justificada não entra no total descontável: Ausência Descontável é
//    falta plena não justificada + saída antecipada; Ausência Justificada é o
//    tempo previsto dos dias abonados, apurado à parte (folha desconta apenas a
//    parcela descontável);
//  - política de tolerância vem do chamador (parâmetro do banco): SOBRE_EXCEDENTE
//    desconta só o que passa da tolerância; ATRASO_COMPLETO desconta o atraso todo
//    quando este ultrapassa a tolerância;
//  - programação semanal acima da carga horária declarada gera pendência
//    PROGRAMACAO_ACIMA_DA_CARGA_SEMANAL (configuração contraditória explícita);
//  - chegada com mais de 12 h além da previsão é interpretada como chegada
//    antecipada (atraso = 0);
//  - CargaHorariaSemanal é a referência declarada da jornada; a janela esperada do
//    dia vem de HoraEntrada/HoraSaida (ou DuracaoPrevistaMinutos quando informada).
// ============================================================================

/// <summary>Política de tolerância de atraso exigida pela regra aprovada (parametrizada por banco).</summary>
public enum ToleranciaPolitica
{
    /// <summary>Só desconta o minuto que ultrapassar a tolerância (comportamento histórico RH-APURACAO-1).</summary>
    SobreExcedente = 0,
    /// <summary>Ultrapassada a tolerância, o atraso inteiro é considerado.</summary>
    AtrasoCompleto = 1
}

public sealed class JornadaPontoRegra
{
    public JornadaPontoRegra(long id, string nome, decimal cargaHorariaSemanal, TimeOnly horaEntrada, TimeOnly horaSaida, int toleranciaMinutos, IEnumerable<int> diasSemana, int? duracaoPrevistaMinutos = null, int? intervaloPrevistoMinutos = null)
    {
        if (id <= 0) throw new ArgumentException("Jornada inválida.", nameof(id));
        if (string.IsNullOrWhiteSpace(nome)) throw new ArgumentException("Nome da jornada é obrigatório.", nameof(nome));
        if (cargaHorariaSemanal < 0m) throw new ArgumentException("Carga horária não pode ser negativa.", nameof(cargaHorariaSemanal));
        if (toleranciaMinutos < 0) throw new ArgumentException("Tolerância de ponto não pode ser negativa.", nameof(toleranciaMinutos));
        if (duracaoPrevistaMinutos is <= 0 or > 1440) throw new ArgumentException("Duração prevista da jornada deve estar entre 1 e 1440 minutos.", nameof(duracaoPrevistaMinutos));
        if (intervaloPrevistoMinutos is < 0 or > 1440) throw new ArgumentException("Intervalo previsto da jornada deve estar entre 0 e 1440 minutos.", nameof(intervaloPrevistoMinutos));
        ArgumentNullException.ThrowIfNull(diasSemana);

        Id = id;
        Nome = nome.Trim();
        CargaHorariaSemanal = cargaHorariaSemanal;
        HoraEntrada = horaEntrada;
        HoraSaida = horaSaida;
        ToleranciaMinutos = toleranciaMinutos;
        DuracaoPrevistaMinutos = duracaoPrevistaMinutos;
        IntervaloPrevistoMinutos = intervaloPrevistoMinutos;
        DiasSemana = diasSemana.Where(d => d is >= 1 and <= 7).Distinct().ToHashSet();
        if (DiasSemana.Count == 0) throw new ArgumentException("Jornada exige ao menos um dia da semana válido (1=segunda .. 7=domingo).", nameof(diasSemana));
    }

    public long Id { get; }
    public string Nome { get; }
    /// <summary>Carga horária semanal declarada da jornada. A janela esperada do dia vem de HoraEntrada/HoraSaida ou DuracaoPrevistaMinutos.</summary>
    public decimal CargaHorariaSemanal { get; }
    public TimeOnly HoraEntrada { get; }
    public TimeOnly HoraSaida { get; }
    public int ToleranciaMinutos { get; }
    /// <summary>Duração diária prevista em minutos, informada explicitamente quando a jornada cobre 24 h; null mantém a leitura da janela horária.</summary>
    public int? DuracaoPrevistaMinutos { get; }
    /// <summary>Intervalo previsto diário em minutos (declarado); usado apenas na relação declarativa carga semanal x programação.</summary>
    public int? IntervaloPrevistoMinutos { get; }
    public IReadOnlySet<int> DiasSemana { get; }
}

public sealed record EscalaPontoResumo(long Id, long JornadaId, DateOnly PeriodoInicio, DateOnly? PeriodoFim, bool Ativa)
{
    public bool Cobre(DateOnly data) => data >= PeriodoInicio && (PeriodoFim is null || data <= PeriodoFim.Value);
}

public sealed record BatidaPonto(long RegistroId, DateTimeOffset DataHora, PontoTipo Tipo, string Origem = "MANUAL");

/// <summary>Período trabalhado efetivamente pareado dentro de um dia (instantes absolutos preservados).</summary>
public sealed record PeriodoTrabalhadoDia(DateTimeOffset Inicio, DateTimeOffset Fim, int IntervaloPareadoMinutos)
{
    public int BrutoMinutos => TruncarMinutos(Fim - Inicio);
    public int TrabalhadoMinutos => Math.Max(0, BrutoMinutos - IntervaloPareadoMinutos);
    internal static int TruncarMinutos(TimeSpan duracao) => (int)(duracao.Ticks / TimeSpan.TicksPerMinute);
}

public sealed record ApuracaoDiaResultado(
    DateOnly Data,
    bool DiaUtil,
    bool Feriado,
    bool SemEscala,
    bool DiaFalta,
    bool AusenciaJustificada,
    int EsperadoMinutos,
    int TrabalhadoMinutos,
    int IntervaloMinutos,
    int AtrasoMinutos,
    int AntecipacaoMinutos,
    int HoraExtraMinutos,
    TimeOnly? EntradaEfetiva,
    TimeOnly? SaidaEfetiva,
    IReadOnlyList<string> Pendencias,
    IReadOnlyList<PeriodoTrabalhadoDia> Periodos,
    int ExcedenteObservadoMinutos,
    int AusenciaJustificadaMinutos,
    int AusenciaDescontavelMinutos,
    bool TemBatidaNoDia = false)
{
    public TimeSpan Trabalhado => TimeSpan.FromMinutes(TrabalhadoMinutos);
    public TimeSpan Atraso => TimeSpan.FromMinutes(AtrasoMinutos);
    public TimeSpan HoraExtra => TimeSpan.FromMinutes(HoraExtraMinutos);
}

public sealed record ApuracaoPontoResultado(
    long ServidorId,
    DateOnly PeriodoInicio,
    DateOnly PeriodoFim,
    string VersaoRegras,
    string FusoHorarioOperacao,
    int DiasUteisPrevistos,
    int DiasSemEscala,
    int DiasFalta,
    int DiasAusenciaJustificada,
    int TotalTrabalhadoMinutos,
    int TotalIntervaloMinutos,
    int TotalAtrasoMinutos,
    int TotalAusenciaMinutos,
    int TotalHoraExtraMinutos,
    IReadOnlyList<ApuracaoDiaResultado> MemoriaPorDia,
    IReadOnlyList<string> PendenciasGlobais,
    int TotalExcedenteObservadoMinutos,
    int TotalAusenciaJustificadaMinutos,
    int TotalAusenciaDescontavelMinutos)
{
    public TimeSpan TotalTrabalhado => TimeSpan.FromMinutes(TotalTrabalhadoMinutos);
    public TimeSpan TotalAtraso => TimeSpan.FromMinutes(TotalAtrasoMinutos);
    public TimeSpan TotalAusencia => TimeSpan.FromMinutes(TotalAusenciaMinutos);
    public TimeSpan TotalHoraExtra => TimeSpan.FromMinutes(TotalHoraExtraMinutos);
    public TimeSpan TotalAusenciaJustificada => TimeSpan.FromMinutes(TotalAusenciaJustificadaMinutos);
    public TimeSpan TotalAusenciaDescontavel => TimeSpan.FromMinutes(TotalAusenciaDescontavelMinutos);
    public bool TemPendencias => PendenciasGlobais.Count > 0;
}

public static class PontoApuracaoEngine
{
    public const string VersaoRegras = "RH-APURACAO-2";
    public const string PendSemEscalaNoDia = "SEM_ESCALA_NO_DIA";
    public const string PendEntradaAusente = "ENTRADA_AUSENTE";
    public const string PendSaidaAusente = "SAIDA_AUSENTE";
    public const string PendIntervaloIncompleto = "INTERVALO_INCOMPLETO";
    public const string PendBatidaForaDeJornada = "BATIDA_FORA_DE_JORNADA";
    public const string PendEntradaConsecutiva = "ENTRADA_CONSECUTIVA";
    public const string PendForaDaJanelaDePareamento = "FORA_DA_JANELA_DE_PAREAMENTO";
    public const string PendTempoSemEscalaObservado = "TEMPO_SEM_ESCALA_OBSERVADO";
    public const string PendProgramacaoAcimaDaCarga = "PROGRAMACAO_ACIMA_DA_CARGA_SEMANAL";
    public const string PendJornadaSemDuracaoExplicita = "JORNADA_SEM_DURACAO_EXPLICITA";

    /// <summary>Janela padrão de pareamento (uma saída mais de 24 h depois da abertura não fecha aquele período).</summary>
    public const int LimitePareamentoMinutosPadrao = 1440;

    public static ToleranciaPolitica? InterpretarPoliticaTolerancia(string? valorConfigurado) =>
        valorConfigurado?.Trim().ToUpperInvariant() switch
        {
            "SOBRE_EXCEDENTE" => ToleranciaPolitica.SobreExcedente,
            "ATRASO_COMPLETO" => ToleranciaPolitica.AtrasoCompleto,
            null or "" => null,
            _ => null
        };

    public static ApuracaoPontoResultado Calcular(
        long servidorId,
        DateOnly periodoInicio,
        DateOnly periodoFim,
        TimeZoneInfo fusoOperacao,
        IReadOnlyList<BatidaPonto> batidas,
        Func<DateOnly, JornadaPontoRegra?> resolverJornada,
        IReadOnlyCollection<DateOnly>? feriados = null,
        IReadOnlyCollection<DateOnly>? ausenciasJustificadas = null,
        ToleranciaPolitica politicaTolerancia = ToleranciaPolitica.SobreExcedente,
        int limitePareamentoMinutos = LimitePareamentoMinutosPadrao)
    {
        if (servidorId <= 0) throw new ArgumentException("Servidor obrigatório para apuração de ponto.", nameof(servidorId));
        if (periodoFim < periodoInicio) throw new ArgumentException("Período final da apuração não pode ser anterior ao inicial.");
        ArgumentNullException.ThrowIfNull(fusoOperacao);
        ArgumentNullException.ThrowIfNull(batidas);
        ArgumentNullException.ThrowIfNull(resolverJornada);
        if (limitePareamentoMinutos <= 0) throw new ArgumentException("Limite de pareamento deve ser positivo.", nameof(limitePareamentoMinutos));

        var feriadosConhecidos = feriados?.OfType<DateOnly>().ToHashSet() ?? new HashSet<DateOnly>();
        var ausenciasJustificadasConhecidas = ausenciasJustificadas?.OfType<DateOnly>().ToHashSet() ?? new HashSet<DateOnly>();

        var pareamento = ParearPorCronologia(batidas, fusoOperacao, limitePareamentoMinutos);

        var memoria = new List<ApuracaoDiaResultado>();
        var pendenciasGlobais = new List<string>();
        var diasUteisPrevistos = 0;
        var diasSemEscala = 0;
        var diasFalta = 0;
        var diasAusenciaJustificada = 0;
        var totalTrabalhado = 0;
        var totalIntervalo = 0;
        var totalAtraso = 0;
        var totalAusencia = 0;
        var totalHoraExtra = 0;
        var totalExcedenteObservado = 0;
        var totalAusenciaJustificada = 0;
        var totalAusenciaDescontavel = 0;
        var jornadasEncontradas = new Dictionary<long, JornadaPontoRegra>();

        for (var data = periodoInicio; data <= periodoFim; data = data.AddDays(1))
        {
            var diaSemanaIso = (int)data.DayOfWeek == 0 ? 7 : (int)data.DayOfWeek;
            var jornada = resolverJornada(data);
            if (jornada is not null) jornadasEncontradas[jornada.Id] = jornada;
            var semEscalaNoDia = jornada is null;
            var diaUtil = jornada is not null && jornada.DiasSemana.Contains(diaSemanaIso);
            var feriado = diaUtil && feriadosConhecidos.Contains(data);
            var temBatidas = pareamento.TemBatidaEm(data);

            var pendenciasDoDia = new List<string>();
            if (pareamento.PendenciasPorDia.TryGetValue(data, out var pendenciasPareamento))
            {
                foreach (var pendencia in pendenciasPareamento)
                    if (!pendenciasDoDia.Contains(pendencia)) pendenciasDoDia.Add(pendencia);
            }

            var periodos = pareamento.PeriodosPorDia.TryGetValue(data, out var periodosDoDia) ? (IReadOnlyList<PeriodoTrabalhadoDia>)periodosDoDia : Array.Empty<PeriodoTrabalhadoDia>();
            var trabalhado = periodos.Sum(p => p.TrabalhadoMinutos);
            var intervalo = periodos.Sum(p => p.IntervaloPareadoMinutos);
            TimeOnly? entradaEfetiva = periodos.Count > 0 ? ParaTimeOnly(MinutosDoLocal(periodos[0].Inicio, fusoOperacao)) : null;
            TimeOnly? saidaEfetiva = periodos.Count > 0 ? ParaTimeOnly(MinutosDoLocal(periodos[^1].Fim, fusoOperacao)) : null;

            var esperado = 0;
            var atraso = 0;
            var antecipacao = 0;
            var horaExtra = 0;
            var excedenteObservado = 0;
            var falta = false;
            var justificada = false;
            var ausenciaJustificadaMin = 0;
            var ausenciaDescontavelMin = 0;

            if (semEscalaNoDia)
            {
                pendenciasDoDia.Add(PendSemEscalaNoDia);
                if (trabalhado > 0)
                {
                    // Fato observado sem escala: registrado como excedente observado com pendência;
                    // NÃO vira hora extra remunerável sem regra aprovada (§3.2).
                    pendenciasDoDia.Add(PendTempoSemEscalaObservado);
                    excedenteObservado = trabalhado;
                }
            }
            else if (!diaUtil || feriado)
            {
                if (trabalhado > 0)
                {
                    pendenciasDoDia.Add(PendBatidaForaDeJornada);
                    horaExtra = trabalhado;
                }
            }
            else
            {
                esperado = JanelaEmMinutos(jornada!, pendenciasDoDia);
                if (!temBatidas)
                {
                    if (ausenciasJustificadasConhecidas.Contains(data))
                    {
                        justificada = true;
                        ausenciaJustificadaMin = esperado;
                    }
                    else
                    {
                        falta = true;
                        ausenciaDescontavelMin = esperado;
                    }
                    totalAusencia += esperado;
                }
                else if (periodos.Count > 0)
                {
                    atraso = CalcularAtraso(
                        MinutosDoLocal(periodos[0].Inicio, fusoOperacao),
                        MinutosDoDia(jornada!.HoraEntrada),
                        jornada.ToleranciaMinutos,
                        politicaTolerancia);
                    var decorrido = PeriodoTrabalhadoDia.TruncarMinutos(periodos[^1].Fim - periodos[0].Inicio);
                    antecipacao = Math.Max(0, esperado - decorrido);
                    if (antecipacao > 0) { totalAusencia += antecipacao; ausenciaDescontavelMin += antecipacao; }
                    horaExtra = Math.Max(0, trabalhado - esperado);
                }
                // periodos.Count == 0 com batidas: só há eventos soltos; as pendências do
                // pareamento (ENTRADA_AUSENTE/SAIDA_AUSENTE/...) já explicam o dia e nenhum
                // minuto é inventado (§3.1: não completar registros ausentes em silêncio).
            }

            if (semEscalaNoDia) diasSemEscala++;
            else if (diaUtil && !feriado) diasUteisPrevistos++;
            if (falta) diasFalta++;
            if (justificada) diasAusenciaJustificada++;
            totalTrabalhado += trabalhado;
            totalIntervalo += intervalo;
            totalAtraso += atraso;
            totalHoraExtra += horaExtra;
            totalExcedenteObservado += excedenteObservado;
            totalAusenciaJustificada += ausenciaJustificadaMin;
            totalAusenciaDescontavel += ausenciaDescontavelMin;
            foreach (var pendencia in pendenciasDoDia)
            {
                if (!pendenciasGlobais.Contains(pendencia)) pendenciasGlobais.Add(pendencia);
            }

            memoria.Add(new ApuracaoDiaResultado(
                data, diaUtil, feriado, semEscalaNoDia, falta, justificada,
                esperado, trabalhado, intervalo, atraso, antecipacao, horaExtra,
                entradaEfetiva, saidaEfetiva, pendenciasDoDia, periodos,
                excedenteObservado, ausenciaJustificadaMin, ausenciaDescontavelMin, temBatidas));
        }

        // §3.2: a relação carga semanal x programação diária só é julgada com insumos
        // DECLARADOS pela jornada (duração prevista ou intervalo previsto). Sem eles a
        // janela horária não permite concluir contradição (envolve o intervalo real), e
        // nada é inventado; com eles, expectativa acima da carga é pendência clara.
        foreach (var jornada in jornadasEncontradas.Values.OrderBy(j => j.Id))
        {
            var minutosDiarios = MinutosDiariosDeclarados(jornada);
            if (minutosDiarios is null) continue;
            var esperadoSemanal = (decimal)minutosDiarios.Value * jornada.DiasSemana.Count / 60m;
            if (esperadoSemanal > jornada.CargaHorariaSemanal + 0.005m && !pendenciasGlobais.Contains(PendProgramacaoAcimaDaCarga))
                pendenciasGlobais.Add(PendProgramacaoAcimaDaCarga);
        }

        return new ApuracaoPontoResultado(
            servidorId, periodoInicio, periodoFim, VersaoRegras, fusoOperacao.Id,
            diasUteisPrevistos, diasSemEscala, diasFalta, diasAusenciaJustificada,
            totalTrabalhado, totalIntervalo, totalAtraso, totalAusencia, totalHoraExtra,
            memoria, pendenciasGlobais,
            totalExcedenteObservado, totalAusenciaJustificada, totalAusenciaDescontavel);
    }

    private sealed record ResultadoPareamento(
        HashSet<DateOnly> DiasComBatida,
        Dictionary<DateOnly, List<PeriodoTrabalhadoDia>> PeriodosPorDia,
        Dictionary<DateOnly, List<string>> PendenciasPorDia)
    {
        public bool TemBatidaEm(DateOnly data) => DiasComBatida.Contains(data);
    }

    /// <summary>
    /// Pareamento cronológico por instante absoluto (§3.1): ordena todos os eventos do
    /// servidor, abre/fecha períodos e intervalos pela sequência válida e devolve a memória
    /// atribuída ao dia local de abertura de cada período, mais as pendências por dia.
    /// </summary>
    private static ResultadoPareamento ParearPorCronologia(
        IReadOnlyList<BatidaPonto> batidas, TimeZoneInfo zona, int limitePareamentoMinutos)
    {
        var periodos = new Dictionary<DateOnly, List<PeriodoTrabalhadoDia>>();
        var pendencias = new Dictionary<DateOnly, List<string>>();
        var diasComBatida = new HashSet<DateOnly>();

        void Marcar(DateOnly data, string pendencia)
        {
            if (!pendencias.TryGetValue(data, out var lista)) pendencias[data] = lista = new List<string>();
            if (!lista.Contains(pendencia)) lista.Add(pendencia);
        }

        // Duplicatas exatas (mesmo tipo no mesmo minuto após truncamento) são o mesmo fato
        // físico do terminal e colapsam; instantes diferentes do mesmo tipo passam a valer
        // como sequência (tratada abaixo com pendência própria quando inválida).
        var eventos = batidas
            .Where(b => b.Tipo != PontoTipo.Ajuste)
            .Select(b => (b.RegistroId, Tipo: b.Tipo, Instante: ParaMinutoAbsoluto(b.DataHora), Dia: DiaLocal(b.DataHora, zona)))
            .OrderBy(e => e.Instante).ThenBy(e => e.RegistroId)
            .GroupBy(e => (e.Tipo, e.Instante))
            .Select(g => g.First())
            .ToList();
        foreach (var e in eventos) diasComBatida.Add(e.Dia);

        DateTimeOffset? periodoAberto = null;
        DateOnly? periodoAbertoDia = null;
        DateTimeOffset? intervaloAberto = null;
        DateOnly? intervaloAbertoDia = null;
        var intervaloAcumulado = 0;

        void FecharIntervaloAbertoSemPar()
        {
            if (intervaloAberto is null) return;
            Marcar(intervaloAbertoDia!.Value, PendIntervaloIncompleto);
            intervaloAberto = null;
            intervaloAbertoDia = null;
        }

        void FecharPeriodo(DateTimeOffset fim)
        {
            var inicio = periodoAberto!.Value;
            if (intervaloAberto is not null) FecharIntervaloAbertoSemPar();
            if (!periodos.TryGetValue(periodoAbertoDia!.Value, out var lista)) periodos[periodoAbertoDia.Value] = lista = new List<PeriodoTrabalhadoDia>();
            lista.Add(new PeriodoTrabalhadoDia(inicio, fim, intervaloAcumulado));
            periodoAberto = null;
            periodoAbertoDia = null;
            intervaloAcumulado = 0;
        }

        foreach (var ev in eventos)
        {
            switch (ev.Tipo)
            {
                case PontoTipo.Entrada:
                    if (periodoAberto is not null)
                    {
                        Marcar(ev.Dia, PendEntradaConsecutiva); // não reabre nem inventa fechamento
                        break;
                    }
                    periodoAberto = ev.Instante;
                    periodoAbertoDia = ev.Dia;
                    intervaloAcumulado = 0;
                    break;

                case PontoTipo.Saida:
                    if (periodoAberto is not null)
                    {
                        var duracao = PeriodoTrabalhadoDia.TruncarMinutos(ev.Instante - periodoAberto.Value);
                        if (duracao > limitePareamentoMinutos)
                        {
                            // Saída além da janela não valida o período inteiro: a abertura fica como
                            // entrada sem saída, o intervalo aberto é marcado e a saída vira evento solto.
                            Marcar(periodoAbertoDia!.Value, PendSaidaAusente);
                            FecharIntervaloAbertoSemPar();
                            periodoAberto = null;
                            periodoAbertoDia = null;
                            intervaloAcumulado = 0;
                            Marcar(ev.Dia, PendEntradaAusente);
                            Marcar(ev.Dia, PendForaDaJanelaDePareamento);
                            break;
                        }
                        FecharPeriodo(ev.Instante);
                        break;
                    }
                    Marcar(ev.Dia, PendEntradaAusente);
                    break;

                case PontoTipo.IntervaloInicio:
                    if (periodoAberto is null || intervaloAberto is not null)
                        Marcar(ev.Dia, PendIntervaloIncompleto);
                    else
                    {
                        intervaloAberto = ev.Instante;
                        intervaloAbertoDia = ev.Dia;
                    }
                    break;

                case PontoTipo.IntervaloFim:
                    if (intervaloAberto is null)
                        Marcar(ev.Dia, PendIntervaloIncompleto);
                    else if (ev.Instante < intervaloAberto.Value)
                        Marcar(ev.Dia, PendIntervaloIncompleto);
                    else
                    {
                        intervaloAcumulado += PeriodoTrabalhadoDia.TruncarMinutos(ev.Instante - intervaloAberto.Value);
                        intervaloAberto = null;
                        intervaloAbertoDia = null;
                    }
                    break;
            }
        }

        if (periodoAberto is not null) Marcar(periodoAbertoDia!.Value, PendSaidaAusente);
        FecharIntervaloAbertoSemPar();

        foreach (var lista in periodos.Values) lista.Sort((a, b) => a.Inicio.CompareTo(b.Inicio));
        return new ResultadoPareamento(diasComBatida, periodos, pendencias);
    }

    private static int CalcularAtraso(int minutosEntradaEfetiva, int minutosPrevisao, int toleranciaMinutos, ToleranciaPolitica politica)
    {
        var delta = (minutosEntradaEfetiva - minutosPrevisao + 1440) % 1440;
        if (delta > 720) return 0; // chegada antecipada
        return politica switch
        {
            ToleranciaPolitica.AtrasoCompleto => delta > toleranciaMinutos ? delta : 0,
            _ => Math.Max(0, delta - toleranciaMinutos)
        };
    }

    private static int JanelaEmMinutos(JornadaPontoRegra jornada, List<string> pendencias)
    {
        if (jornada.DuracaoPrevistaMinutos is int duracaoExplicita) return duracaoExplicita;
        var janela = (MinutosDoDia(jornada.HoraSaida) - MinutosDoDia(jornada.HoraEntrada) + 1440) % 1440;
        if (janela == 0)
        {
            // Entrada == saída sem duração declarada: não assume 24 h silenciosamente (§3.2).
            pendencias.Add(PendJornadaSemDuracaoExplicita);
            return 1440;
        }
        return janela;
    }

    /// <summary>
    /// Tempo de trabalho diário APENAS com insumos declarados: duração prevista explícita,
    /// ou janela menos intervalo previsto. null = a jornada não declara o suficiente para a
    /// relação carga semanal x programação; nada é presumido (§3.2).
    /// </summary>
    private static int? MinutosDiariosDeclarados(JornadaPontoRegra jornada)
    {
        if (jornada.DuracaoPrevistaMinutos is int duracao) return duracao;
        if (jornada.IntervaloPrevistoMinutos is int intervalo)
            return Math.Max(0, JanelaEmMinutos(jornada, new List<string>()) - intervalo);
        return null;
    }

    private static DateTimeOffset ParaMinutoAbsoluto(DateTimeOffset instante) =>
        new(instante.Ticks - instante.Ticks % TimeSpan.TicksPerMinute, instante.Offset);

    private static DateOnly DiaLocal(DateTimeOffset instante, TimeZoneInfo zona) =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(instante.UtcDateTime, zona));

    private static int MinutosDoLocal(DateTimeOffset instante, TimeZoneInfo zona)
    {
        var local = TimeZoneInfo.ConvertTimeFromUtc(instante.UtcDateTime, zona);
        return local.Hour * 60 + local.Minute;
    }

    private static int MinutosDoDia(TimeOnly hora) => hora.Hour * 60 + hora.Minute;
    private static TimeOnly ParaTimeOnly(int minutos) => new(minutos / 60 % 24, minutos % 60);
}
