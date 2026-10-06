namespace Sigov.Domain.Rh;

/// <summary>
/// RC-EVO-RH §5: regras puras de decisão de justificativas e ajuste de batidas de ponto.
/// O backend é a única autoridade sobre transições; o cliente apenas antecipa UX.
/// </summary>
public static class PontoTransicoes
{
    public const string TransicaoInvalida = "TRANSICAO_INVALIDA";
    public const string AutoprovacaoBloqueada = "AUTOPROVACAO_BLOQUEADA";

    public const string Aprovada = "APROVADA";
    public const string Reprovada = "REPROVADA";

    // Estados em que a justificativa ainda aguarda decisão (a coluna padrão nasce 'RASCUNHO').
    private static readonly HashSet<string> EstadosPendentes = new(StringComparer.OrdinalIgnoreCase)
    {
        string.Empty, "RASCUNHO", "PENDENTE", "ANALISE"
    };

    /// <summary>Transição válida para decisão: retorna null quando válida ou a falha nomeada.</summary>
    public static string? ValidarDecisao(string? statusAtual)
        => EstadosPendentes.Contains(statusAtual ?? string.Empty) ? null : TransicaoInvalida;

    /// <summary>Por padrão o autor do registro não decide a própria justificativa (bloqueio só quando ambos os ids são conhecidos).</summary>
    public static string? ValidarAutoprovacao(long? autorId, long? decisorId)
        => autorId.HasValue && decisorId.HasValue && autorId.Value == decisorId.Value ? AutoprovacaoBloqueada : null;

    /// <summary>
    /// Janela de competências afetadas por um ajuste de batida: datas UTC do instante antes/depois
    /// ampliadas em ±1 dia para cobrir deslocamentos de fuso operacional sem adivinhar o offset.
    /// </summary>
    public static (DateOnly Inicio, DateOnly Fim) JanelaAjuste(DateTimeOffset antes, DateTimeOffset depois)
    {
        var menor = antes <= depois ? antes : depois;
        var maior = antes <= depois ? depois : antes;
        return (DateOnly.FromDateTime(menor.UtcDateTime).AddDays(-1), DateOnly.FromDateTime(maior.UtcDateTime).AddDays(1));
    }

    /// <summary>Ajuste exige tipo de batida reconhecível (sem inventar categorias).</summary>
    public static bool TipoBatidaValido(string? tipo)
        => !string.IsNullOrWhiteSpace(tipo) && Enum.TryParse<PontoTipo>(tipo!, true, out _);
}
