namespace Sigov.Domain.Rh;

// ============================================================================
// RC-EVO-RH §4 - Engine de apuração real de ponto.
// C# puro, sem persistência: recebe a regra da jornada, as batidas, os feriados e
// as ausências justificadas aprovadas; devolve o resultado agregado + memória por
// dia, sempre em minutos/TimeSpan (nunca "1,30 horas"). A versão das regras é
// explícita (VersaoRegras) e deve ser gravada junto de cada apuração calculada.
// Nenhuma legislação fica embutida: apenas aritmética entre batidas registradas e
// a jornada configurada. Tolerância, feriados e justificativas são entradas
// fornecidas pelo chamador (autoridade do banco), parametrizadas por jornada.
// Convenções documentadas:
//  - diasSemana segue ISO-8601 (1 = segunda .. 7 = domingo);
//  - segundos das batidas são truncados para minutos;
//  - virada de dia: uma Saida cujo dia local não possui Entrada e cujo dia anterior
//    possui Entrada sem Saida é atribuída ao dia anterior (turno noturno);
//  - batida com tipo Ajuste é marcador de registro ajustado (o ajuste materializa
//    nova dataHora/tipo) e não conta como entrada/saída/intervalo por si só;
//  - ausência justificada remove apenas a penalidade da falta plena sem batidas;
//  - chegada com mais de 12 h além da previsão é interpretada como chegada
//    antecipada (atraso = 0);
//  - CargaHorariaSemanal é informativa; a janela esperada do dia vem de
//    HoraEntrada/HoraSaida da jornada.
// ============================================================================

public sealed class JornadaPontoRegra
{
    public JornadaPontoRegra(long id, string nome, decimal cargaHorariaSemanal, TimeOnly horaEntrada, TimeOnly horaSaida, int toleranciaMinutos, IEnumerable<int> diasSemana)
    {
        if (id <= 0) throw new ArgumentException("Jornada inválida.", nameof(id));
        if (string.IsNullOrWhiteSpace(nome)) throw new ArgumentException("Nome da jornada é obrigatório.", nameof(nome));
        if (cargaHorariaSemanal < 0m) throw new ArgumentException("Carga horária não pode ser negativa.", nameof(cargaHorariaSemanal));
        if (toleranciaMinutos < 0) throw new ArgumentException("Tolerância de ponto não pode ser negativa.", nameof(toleranciaMinutos));
        ArgumentNullException.ThrowIfNull(diasSemana);

        Id = id;
        Nome = nome.Trim();
        CargaHorariaSemanal = cargaHorariaSemanal;
        HoraEntrada = horaEntrada;
        HoraSaida = horaSaida;
        ToleranciaMinutos = toleranciaMinutos;
        DiasSemana = diasSemana.Where(d => d is >= 1 and <= 7).Distinct().ToHashSet();
        if (DiasSemana.Count == 0) throw new ArgumentException("Jornada exige ao menos um dia da semana válido (1=segunda .. 7=domingo).", nameof(diasSemana));
    }

    public long Id { get; }
    public string Nome { get; }
    /// <summary>Carga horária semanal de referência (informativa). A janela esperada do dia vem de HoraEntrada/HoraSaida.</summary>
    public decimal CargaHorariaSemanal { get; }
    public TimeOnly HoraEntrada { get; }
    public TimeOnly HoraSaida { get; }
    public int ToleranciaMinutos { get; }
    public IReadOnlySet<int> DiasSemana { get; }
}

public sealed record EscalaPontoResumo(long Id, long JornadaId, DateOnly PeriodoInicio, DateOnly? PeriodoFim, bool Ativa)
{
    public bool Cobre(DateOnly data) => data >= PeriodoInicio && (PeriodoFim is null || data <= PeriodoFim.Value);
}

public sealed record BatidaPonto(long RegistroId, DateTimeOffset DataHora, PontoTipo Tipo, string Origem = "MANUAL");

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
    IReadOnlyList<string> Pendencias)
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
    IReadOnlyList<string> PendenciasGlobais)
{
    public TimeSpan TotalTrabalhado => TimeSpan.FromMinutes(TotalTrabalhadoMinutos);
    public TimeSpan TotalAtraso => TimeSpan.FromMinutes(TotalAtrasoMinutos);
    public TimeSpan TotalAusencia => TimeSpan.FromMinutes(TotalAusenciaMinutos);
    public TimeSpan TotalHoraExtra => TimeSpan.FromMinutes(TotalHoraExtraMinutos);
    public bool TemPendencias => PendenciasGlobais.Count > 0;
}

public static class PontoApuracaoEngine
{
    public const string VersaoRegras = "RH-APURACAO-1";
    public const string PendSemEscalaNoDia = "SEM_ESCALA_NO_DIA";
    public const string PendEntradaAusente = "ENTRADA_AUSENTE";
    public const string PendSaidaAusente = "SAIDA_AUSENTE";
    public const string PendIntervaloIncompleto = "INTERVALO_INCOMPLETO";
    public const string PendBatidaForaDeJornada = "BATIDA_FORA_DE_JORNADA";

    public static ApuracaoPontoResultado Calcular(
        long servidorId,
        DateOnly periodoInicio,
        DateOnly periodoFim,
        TimeZoneInfo fusoOperacao,
        IReadOnlyList<BatidaPonto> batidas,
        Func<DateOnly, JornadaPontoRegra?> resolverJornada,
        IReadOnlyCollection<DateOnly>? feriados = null,
        IReadOnlyCollection<DateOnly>? ausenciasJustificadas = null)
    {
        if (servidorId <= 0) throw new ArgumentException("Servidor obrigatório para apuração de ponto.", nameof(servidorId));
        if (periodoFim < periodoInicio) throw new ArgumentException("Período final da apuração não pode ser anterior ao inicial.");
        ArgumentNullException.ThrowIfNull(fusoOperacao);
        ArgumentNullException.ThrowIfNull(batidas);
        ArgumentNullException.ThrowIfNull(resolverJornada);

        var feriadosConhecidos = feriados?.OfType<DateOnly>().ToHashSet() ?? new HashSet<DateOnly>();
        var ausenciasJustificadasConhecidas = ausenciasJustificadas?.OfType<DateOnly>().ToHashSet() ?? new HashSet<DateOnly>();

        var porDia = new Dictionary<DateOnly, List<BatidaPonto>>();
        foreach (var batida in batidas)
        {
            var dataLocal = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(batida.DataHora.UtcDateTime, fusoOperacao));
            if (!porDia.TryGetValue(dataLocal, out var lista)) porDia[dataLocal] = lista = new List<BatidaPonto>();
            lista.Add(batida);
        }

        AplicarViradaDeDia(porDia);

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

        for (var data = periodoInicio; data <= periodoFim; data = data.AddDays(1))
        {
            var diaSemanaIso = (int)data.DayOfWeek == 0 ? 7 : (int)data.DayOfWeek;
            var jornada = resolverJornada(data);
            var semEscalaNoDia = jornada is null;
            var diaUtil = jornada is not null && jornada.DiasSemana.Contains(diaSemanaIso);
            var feriado = diaUtil && feriadosConhecidos.Contains(data);
            var batidasDoDia = porDia.TryGetValue(data, out var batidasRegistradas) ? batidasRegistradas : new List<BatidaPonto>();

            var pendenciasDoDia = new List<string>();
            var analisado = AnalisarBatidas(batidasDoDia, fusoOperacao, pendenciasDoDia);
            var esperado = 0;
            var atraso = 0;
            var antecipacao = 0;
            var horaExtra = 0;
            var falta = false;
            var justificada = false;

            if (semEscalaNoDia)
            {
                pendenciasDoDia.Add(PendSemEscalaNoDia);
                esperado = JanelaEmMinutosDaData(data, resolverJornada);
                // Fato: tudo que foi trabalhado sem escala definida conta como extra.
                horaExtra = analisado.TrabalhadoMin;
            }
            else if (!diaUtil || feriado)
            {
                if (analisado.TrabalhadoMin > 0)
                {
                    pendenciasDoDia.Add(PendBatidaForaDeJornada);
                    horaExtra = analisado.TrabalhadoMin;
                }
            }
            else
            {
                esperado = JanelaEmMinutos(jornada!);
                var temBatidas = batidasDoDia.Count > 0;
                if (!temBatidas)
                {
                    totalAusencia += esperado;
                    if (ausenciasJustificadasConhecidas.Contains(data)) justificada = true;
                    else falta = true;
                }
                else
                {
                    if (analisado.Entrada is null) pendenciasDoDia.Add(PendEntradaAusente);
                    if (analisado.Saida is null) pendenciasDoDia.Add(PendSaidaAusente);
                    if (analisado.Entrada is not null && analisado.Saida is not null)
                    {
                        atraso = CalcularAtraso(MinutosDoDia(analisado.Entrada.Value), MinutosDoDia(jornada!.HoraEntrada), jornada.ToleranciaMinutos);
                        var decorrido = (MinutosDoDia(analisado.Saida.Value) - MinutosDoDia(jornada.HoraEntrada) + 1440) % 1440;
                        antecipacao = Math.Max(0, esperado - decorrido);
                        horaExtra = Math.Max(0, analisado.TrabalhadoMin - esperado);
                        totalAusencia += antecipacao;
                    }
                }
            }

            if (semEscalaNoDia) diasSemEscala++;
            else if (diaUtil && !feriado) diasUteisPrevistos++;
            if (falta) diasFalta++;
            if (justificada) diasAusenciaJustificada++;
            totalTrabalhado += analisado.TrabalhadoMin;
            totalIntervalo += analisado.IntervaloMin;
            totalAtraso += atraso;
            totalHoraExtra += horaExtra;
            foreach (var pendencia in pendenciasDoDia)
            {
                if (!pendenciasGlobais.Contains(pendencia)) pendenciasGlobais.Add(pendencia);
            }

            memoria.Add(new ApuracaoDiaResultado(
                data, diaUtil, feriado, semEscalaNoDia, falta, justificada,
                esperado, analisado.TrabalhadoMin, analisado.IntervaloMin, atraso, antecipacao, horaExtra,
                analisado.Entrada, analisado.Saida, pendenciasDoDia));
        }

        return new ApuracaoPontoResultado(
            servidorId, periodoInicio, periodoFim, VersaoRegras, fusoOperacao.Id,
            diasUteisPrevistos, diasSemEscala, diasFalta, diasAusenciaJustificada,
            totalTrabalhado, totalIntervalo, totalAtraso, totalAusencia, totalHoraExtra,
            memoria, pendenciasGlobais);
    }

    /// <summary>Dia sem escala não tem jornada esperável; a memória registra 0 esperado (o pendício SEM_ESCALA_NO_DIA é a falha identificável).</summary>
    private static int JanelaEmMinutosDaData(DateOnly data, Func<DateOnly, JornadaPontoRegra?> resolverJornada)
    {
        var jornada = resolverJornada(data);
        return jornada is null ? 0 : JanelaEmMinutos(jornada);
    }

    private static void AplicarViradaDeDia(Dictionary<DateOnly, List<BatidaPonto>> porDia)
    {
        foreach (var data in porDia.Keys.OrderBy(d => d).ToList())
        {
            if (!porDia.TryGetValue(data, out var listaDia)) continue;
            var saidas = listaDia.Where(b => b.Tipo == PontoTipo.Saida).ToList();
            if (saidas.Count == 0 || listaDia.Any(b => b.Tipo == PontoTipo.Entrada)) continue;
            var anterior = data.AddDays(-1);
            if (!porDia.TryGetValue(anterior, out var listaAnterior)) continue;
            if (!listaAnterior.Any(b => b.Tipo == PontoTipo.Entrada) || listaAnterior.Any(b => b.Tipo == PontoTipo.Saida)) continue;
            foreach (var saida in saidas)
            {
                listaDia.Remove(saida);
                listaAnterior.Add(saida);
            }
        }
    }

    private static (TimeOnly? Entrada, TimeOnly? Saida, int BrutoMin, int IntervaloMin, int TrabalhadoMin) AnalisarBatidas(
        IReadOnlyList<BatidaPonto> lista, TimeZoneInfo zona, List<string> pendencias)
    {
        var entradas = new List<int>();
        var saidas = new List<int>();
        var intervaloInicios = new List<int>();
        var intervalosFins = new List<int>();

        foreach (var batida in lista)
        {
            var local = TimeZoneInfo.ConvertTimeFromUtc(batida.DataHora.UtcDateTime, zona);
            var minutos = local.Hour * 60 + local.Minute;
            switch (batida.Tipo)
            {
                case PontoTipo.Entrada: entradas.Add(minutos); break;
                case PontoTipo.Saida: saidas.Add(minutos); break;
                case PontoTipo.IntervaloInicio: intervaloInicios.Add(minutos); break;
                case PontoTipo.IntervaloFim: intervalosFins.Add(minutos); break;
                default: break; // Ajuste: já materializado como nova batida no ajuste
            }
        }

        TimeOnly? entrada = null;
        TimeOnly? saida = null;
        if (entradas.Count > 0) entrada = ParaTimeOnly(entradas.Min());
        if (saidas.Count > 0) saida = ParaTimeOnly(saidas.Max());

        intervaloInicios.Sort();
        intervalosFins.Sort();
        var minutosIntervalo = 0;
        if (intervaloInicios.Count != intervalosFins.Count) pendencias.Add(PendIntervaloIncompleto);
        for (var i = 0; i < Math.Min(intervaloInicios.Count, intervalosFins.Count); i++)
        {
            if (intervalosFins[i] > intervaloInicios[i]) minutosIntervalo += intervalosFins[i] - intervaloInicios[i];
            else if (!pendencias.Contains(PendIntervaloIncompleto)) pendencias.Add(PendIntervaloIncompleto);
        }

        var bruto = entrada is not null && saida is not null
            ? (MinutosDoDia(saida.Value) - MinutosDoDia(entrada.Value) + 1440) % 1440
            : 0;
        return (entrada, saida, bruto, minutosIntervalo, Math.Max(0, bruto - minutosIntervalo));
    }

    private static int CalcularAtraso(int minutosEntradaEfetiva, int minutosPrevisao, int toleranciaMinutos)
    {
        var delta = (minutosEntradaEfetiva - minutosPrevisao + 1440) % 1440;
        return delta > 720 ? 0 : Math.Max(0, delta - toleranciaMinutos);
    }

    private static int JanelaEmMinutos(JornadaPontoRegra jornada)
    {
        var janela = (MinutosDoDia(jornada.HoraSaida) - MinutosDoDia(jornada.HoraEntrada) + 1440) % 1440;
        return janela == 0 ? 1440 : janela;
    }

    private static int MinutosDoDia(TimeOnly hora) => hora.Hour * 60 + hora.Minute;
    private static TimeOnly ParaTimeOnly(int minutos) => new(minutos / 60 % 24, minutos % 60);
}
