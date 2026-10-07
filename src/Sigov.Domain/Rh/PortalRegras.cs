using System.Globalization;
using System.Text.Json;

namespace Sigov.Domain.Rh;

/// <summary>
/// RC-EVO-RH §8: regras puras do espelho de folha no portal RH com escopo próprio.
/// O portal nunca aceita o servidor via payload: o vínculo usuário→servidor é resolvido no
/// servidor (sigov.rh_portal_usuario) e todo registro é verificado contra esse escopo
/// (fail-closed). Totais são sempre recalculados a partir dos lançamentos materializados:
/// valor positivo, sinal definido exclusivamente pelo tipo — sem valor de sinal inventado.
/// Dado ausente produz null explícita, nunca sucesso simulado.
/// </summary>
public static class PortalRegras
{
    // ==== Falhas nomeadas =====================================================

    /// <summary>Vínculo portal (usuário→servidor) inexistente ou inválido no tenant.</summary>
    public const string FalhaVinculoAusente = "VINCULO_PORTAL_NAO_ENCONTRADO";

    /// <summary>Lançamento de folha pertence a outro servidor (fora do escopo do usuário).</summary>
    public const string FalhaLancamentoForaDoEscopo = "LANCAMENTO_FORA_DO_ESCOPO";

    // ==== Escopo próprio ======================================================

    /// <summary>
    /// Servidor id persistido em JSONB (camelCase da engine ou PascalCase da API).
    /// Valor ausente ou não reconhecido → null (fail-closed).
    /// </summary>
    public static long? LerServidorId(string? servidorIdTextual)
        => long.TryParse((servidorIdTextual ?? string.Empty).Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var valor) ? valor : null;

    /// <summary>Verificação de pertencimento: o registro pertence ao servidor esperado. Ausência jamais passa.</summary>
    public static bool PertenceAoServidor(string? servidorIdTextual, long servidorEsperado)
        => LerServidorId(servidorIdTextual) == servidorEsperado;

    // ==== Totais recalculados =================================================

    /// <summary>
    /// Totais reais dos lançamentos materializados: valor positivo (Math.Abs) e sinal definido
    /// somente pelo tipo PROVENTO/DESCONTO (case-insensitive, constantes de <see cref="FolhaRegras"/>);
    /// tipo não reconhecido contribui zero — sem sinal inventado. Líquido = proventos − descontos.
    /// </summary>
    public static (decimal Proventos, decimal Descontos, decimal Liquido) TotaisPorTipo(IReadOnlyList<(string Tipo, decimal Valor)> lancamentos)
    {
        decimal proventos = 0m;
        decimal descontos = 0m;
        if (lancamentos is not null)
        {
            foreach (var (tipo, valor) in lancamentos)
            {
                var valorPositivo = Math.Abs(valor);
                if (string.Equals(tipo?.Trim(), FolhaRegras.RubricaProvento, StringComparison.OrdinalIgnoreCase)) proventos += valorPositivo;
                else if (string.Equals(tipo?.Trim(), FolhaRegras.RubricaDesconto, StringComparison.OrdinalIgnoreCase)) descontos += valorPositivo;
            }
        }
        return (proventos, descontos, proventos - descontos);
    }

    // ==== Pendências do portal ================================================

    /// <summary>Apuração pendente: somente APURADA (reaberta volta a APURADA); HOMOLOGADA está fechada.</summary>
    public static bool ApuracaoPendente(string? status)
        => string.Equals(status?.Trim(), PontoTransicoes.Apurada, StringComparison.OrdinalIgnoreCase);

    /// <summary>Justificativa pendente: ainda aguarda decisão (§5 — APROVADA/REPROVADA encerram).</summary>
    public static bool JustificativaPendente(string? status)
        => PontoTransicoes.ValidarDecisao(status) is null;

    /// <summary>Integração de folha pendente: nem processada (PROCESSADA) nem encerrada (CANCELADA).</summary>
    public static bool IntegracaoPendente(string? status)
        => !string.Equals(status?.Trim(), FolhaRegras.IntegracaoProcessada, StringComparison.OrdinalIgnoreCase)
           && !string.Equals(status?.Trim(), FolhaRegras.IntegracaoCancelada, StringComparison.OrdinalIgnoreCase);

    /// <summary>Classifica se um item listado é pendência do portal por tipo. Tipo desconhecido não é pendência.</summary>
    public static bool EhPendencia(string tipo, string? status) => tipo switch
    {
        "APURACAO" => ApuracaoPendente(status),
        "JUSTIFICATIVA" => JustificativaPendente(status),
        "INTEGRACAO_FOLHA" => IntegracaoPendente(status),
        _ => false
    };

    // ==== Leitura defensiva de JSONB ==========================================

    /// <summary>
    /// Memória por dia lida case-insensitive do payload da apuração.
    /// Ausente ou malformado → null (explícito, nunca sucesso simulado).
    /// O elemento retornado é um clone independente: pode ser guardado além do
    /// tempo de vida do JsonDocument de origem (a resposta o serializa depois do dispose).
    /// </summary>
    public static JsonElement? MemoriaPorDia(JsonElement dados)
    {
        if (dados.ValueKind != JsonValueKind.Object) return null;
        foreach (var propriedade in dados.EnumerateObject())
        {
            if (!string.Equals(propriedade.Name, "memoriaPorDia", StringComparison.OrdinalIgnoreCase)) continue;
            return propriedade.Value.ValueKind is JsonValueKind.Array or JsonValueKind.Object ? propriedade.Value.Clone() : null;
        }
        return null;
    }

    /// <summary>
    /// Competência exibível "yyyy-MM": ano/mês válidos da folha têm prioridade; caso contrário,
    /// o início do período apurado. Null = não deriva.
    /// </summary>
    public static string? Competencia(int? anoFolha, int? mesFolha, DateOnly? periodoInicio)
    {
        if (anoFolha.HasValue && anoFolha.Value > 0 && mesFolha.HasValue && mesFolha.Value >= 1 && mesFolha.Value <= 13)
            return $"{anoFolha.Value:D4}-{mesFolha.Value:D2}";
        if (periodoInicio.HasValue) return periodoInicio.Value.ToString("yyyy-MM");
        return null;
    }
}
