using System.Globalization;
using System.Text.Json;

namespace Sigov.Domain.Rh;

/// <summary>
/// RC-EVO-RH §9: regras puras da relação Folha de ponto → Financeiro (fila → documento).
/// Puro por design: parse, validação e plano do empenho vivem aqui para os testes unitários
/// cobrirem a decisão sem infraestrutura. Toda falha é nomeada e explícita — ausente nunca
/// vira valor fictício nem sucesso simulado (regra 13 dos AGENTS.md).
/// O consumidor só age com regras suficientes (ORCAMENTO_DESPESA_FOLHA_ID + FORNECEDOR_FOLHA_ID);
/// sem elas o evento falha nomeado. RC-EVO-B §5: falhas definitivas nomeadas (configuração, payload,
/// checksum, saldo) viram dead-letter rastreável (FALHOU) em vez de backoff infinito; transitórias
/// continuam com retry. A data de emissão do documento é congelada no payload na publicação e o
/// payload circula assinado (payloadHash) para detecção de divergência entre publicar e consumir.
/// </summary>
public static class FolhaPontoFinanceiraRegras
{
    // ==== Constantes de integração ==================================================

    /// <summary>Tipo do evento no outbox que este handler consome.</summary>
    public const string TipoEvento = "RH_FOLHA_PONTO_FINANCEIRO";

    /// <summary>Agregado raiz do evento no outbox.</summary>
    public const string AgregadoTipo = "rh-ponto-integracao-folha";

    /// <summary>Tipo de empenho PADRÃO quando o parâmetro TIPO_EMPENHO_INTEGRACAO_PONTO está ausente
    /// (vocabulário já usado pela UI Financeiro e pelos dados dev).</summary>
    public const string TipoEmpenho = "ORDINARIO";

    /// <summary>Parâmetro FOLHA: tipo de empenho usado pela integração de ponto (default ORDINARIO).</summary>
    public const string ParametroTipoEmpenho = "TIPO_EMPENHO_INTEGRACAO_PONTO";

    /// <summary>Parâmetro FOLHA: habilita/desabilita a publicação na fila financeira na integração.</summary>
    public const string ParametroHabilitar = "HABILITAR_INTEGRACAO_FINANCEIRA";

    /// <summary>Parâmetro FOLHA: dotação orçamentária onde o custo do ponto é empenhado.</summary>
    public const string ParametroOrcamento = "ORCAMENTO_DESPESA_FOLHA_ID";

    /// <summary>Parâmetro FOLHA: pessoa pagadora (fornecedor) do empenho de folha de ponto.</summary>
    public const string ParametroFornecedor = "FORNECEDOR_FOLHA_ID";

    // ==== Falhas nomeadas ===========================================================

    public const string FalhaPayloadInvalido = "PAYLOAD_FOLHA_PONTO_INVALIDO";
    public const string FalhaSemProventos = "SEM_PROVENTOS_PARA_EMPENHO";
    public const string FalhaRegrasInsuficientes = "REGRAS_FINANCEIRAS_INSUFICIENTES";
    public const string FalhaExercicioAusente = "EXERCICIO_AUSENTE";
    // RC-EVO-B §5: integridade do payload entre publicação e consumo (checksum persistido na origem).
    public const string FalhaHashAusente = "PAYLOAD_FINANCEIRO_SEM_HASH";
    public const string FalhaChecksumDivergente = "PAYLOAD_FINANCEIRO_CHECKSUM_DIVERGENTE";

    /// <summary>Tipo de lançamento que alimenta o empenho (proventos; descontos ficam apenas nas observações).</summary>
    public const string RubricaProvento = "PROVENTO";

    // ==== Chaves e dinheiro =========================================================

    /// <summary>
    /// Chave única por tenant+integração. O índice único parcial do outbox_evento NÃO é por
    /// tenant, então o tenant entra na chave; producer (fila) e consumer (empenho, escopo
    /// 'empenho.criar') compartilham a mesma chave → retry não duplica documento.
    /// </summary>
    public static string ChaveIntegracao(long tenantId, long integracaoId) =>
        $"rh.folha-ponto-{tenantId.ToString(CultureInfo.InvariantCulture)}-{integracaoId.ToString(CultureInfo.InvariantCulture)}";

    /// <summary>Arredondamento de dinheiro — espelha FinanceiroInvariantes.Money (2 casas, afastado de zero).</summary>
    public static decimal Money(decimal valor) => Math.Round(valor, 2, MidpointRounding.AwayFromZero);

    // ==== Interpretação de parâmetros ===============================================

    /// <summary>
    /// Interpreta o valor JSONB de um parâmetro INTEGER como long fail-closed: null/ausente/
    /// inválido → 0 (regra insuficiente). Aceita número JSON, texto numérico e "null".
    /// </summary>
    public static long InterpretarParametroLong(string? valueJson)
    {
        if (string.IsNullOrWhiteSpace(valueJson)) return 0;
        var texto = valueJson.Trim();
        if (string.Equals(texto, "null", StringComparison.OrdinalIgnoreCase)) return 0;
        if (long.TryParse(texto, NumberStyles.Integer, CultureInfo.InvariantCulture, out var direto)) return direto;
        if (texto.Length >= 2 && texto[0] == '"' && texto[^1] == '"' &&
            long.TryParse(texto.AsSpan(1, texto.Length - 2), NumberStyles.Integer, CultureInfo.InvariantCulture, out var entreAspas))
        {
            return entreAspas;
        }
        try
        {
            using var doc = JsonDocument.Parse(texto);
            switch (doc.RootElement.ValueKind)
            {
                case JsonValueKind.Number when doc.RootElement.TryGetInt64(out var numero):
                    return numero;
                case JsonValueKind.String when long.TryParse(doc.RootElement.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var comoTexto):
                    return comoTexto;
                default:
                    return 0;
            }
        }
        catch (JsonException)
        {
            return 0;
        }
    }

    /// <summary>
    /// RC-EVO-B §5: tipo de empenho da integração lido do parâmetro FOLHA
    /// TIPO_EMPENHO_INTEGRACAO_PONTO. Aceita JSON string/texto/aspas; ausente ou ilegível
    /// → default ORDINARIO (política aprovada; não é valor inventado, é o padrão documentado).
    /// </summary>
    public static string InterpretarTipoEmpenho(string? valueJson)
    {
        if (string.IsNullOrWhiteSpace(valueJson)) return TipoEmpenho;
        var texto = valueJson.Trim();
        if (texto.Length >= 2 && texto[0] == '"' && texto[^1] == '"')
        {
            try
            {
                using var doc = JsonDocument.Parse(texto);
                if (doc.RootElement.ValueKind == JsonValueKind.String) texto = doc.RootElement.GetString() ?? string.Empty;
            }
            catch (JsonException)
            {
                texto = texto[1..^1];
            }
        }
        texto = texto.Trim().ToUpperInvariant();
        return texto.Length == 0 ? TipoEmpenho : texto;
    }

    /// <summary>Verifica as regras suficientes da integração; null = suficiente; senão mensagem nomeada listando as ausências.</summary>
    public static string? ValidarRegras(long orcamentoDespesaId, long fornecedorPessoaId)
    {
        var ausentes = new List<string>();
        if (orcamentoDespesaId <= 0) ausentes.Add($"parâmetro {ParametroOrcamento} (dotação orçamentária) não definido");
        if (fornecedorPessoaId <= 0) ausentes.Add($"parâmetro {ParametroFornecedor} (pessoa pagadora) não definido");
        return ausentes.Count == 0
            ? null
            : $"{FalhaRegrasInsuficientes}: regras de integração financeira insuficientes — {string.Join("; ", ausentes)}; defina-os no módulo FOLHA antes de consumir.";
    }

    // ==== Payload da fila ===========================================================

    public sealed record ItemPayload(string Codigo, string Nome, string Tipo, decimal Valor);

    public sealed record FolhaPontoFinanceiraPayload(
        long IntegracaoId,
        long ApuracaoId,
        long FolhaId,
        long ServidorId,
        string VersaoRegras,
        DateOnly PeriodoInicio,
        DateOnly PeriodoFim,
        int CompetenciaAno,
        int CompetenciaMes,
        long EntidadeId,
        long ExercicioId,
        decimal TotalProventos,
        decimal TotalDescontos,
        long? UsuarioId,
        // RC-EVO-B §5: data de emissão congelada na PUBLICAÇÃO (documento não pode mudar de data
        // conforme o dia em que o retry for consumido) e checksum do payload assinado na origem.
        DateOnly DataEmissao,
        string Hash,
        IReadOnlyList<ItemPayload> Itens);

    /// <summary>Parse case-insensitive do payload (producer grava camelCase; aceita legacy PascalCase).
    /// Incompleto lança <see cref="InvalidOperationException"/> nomeada — nunca valor inventado.</summary>
    public static FolhaPontoFinanceiraPayload Parse(string? payloadJson)
    {
        if (string.IsNullOrWhiteSpace(payloadJson))
            throw new InvalidOperationException($"{FalhaPayloadInvalido}: payload vazio.");

        JsonElement root;
        try
        {
            using var doc = JsonDocument.Parse(payloadJson);
            root = doc.RootElement.Clone();
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException($"{FalhaPayloadInvalido}: payload não é um JSON válido. ({ex.Message})", ex);
        }
        if (root.ValueKind != JsonValueKind.Object)
            throw new InvalidOperationException($"{FalhaPayloadInvalido}: esperado objeto JSON no payload.");

        var itens = new List<ItemPayload>();
        if (TryGetArray(root, out var array))
        {
            foreach (var elemento in array.EnumerateArray())
            {
                if (elemento.ValueKind != JsonValueKind.Object)
                    throw new InvalidOperationException($"{FalhaPayloadInvalido}: item inválido dentro de 'itens'.");
                itens.Add(new ItemPayload(
                    JsonTexto(elemento, "codigo", "Codigo"),
                    JsonTexto(elemento, "nome", "Nome"),
                    JsonTexto(elemento, "tipo", "Tipo"),
                    JsonDecimal(elemento, "valor", "Valor")));
            }
        }

        return new FolhaPontoFinanceiraPayload(
            ExigirLong(root, nameof(FolhaPontoFinanceiraPayload.IntegracaoId), "integracaoId", "IntegracaoId"),
            ExigirLong(root, nameof(FolhaPontoFinanceiraPayload.ApuracaoId), "apuracaoId", "ApuracaoId"),
            ExigirLong(root, nameof(FolhaPontoFinanceiraPayload.FolhaId), "folhaId", "FolhaId"),
            ExigirLong(root, nameof(FolhaPontoFinanceiraPayload.ServidorId), "servidorId", "ServidorId"),
            JsonTexto(root, "versaoRegras", "VersaoRegras"),
            ExigirData(root, nameof(FolhaPontoFinanceiraPayload.PeriodoInicio), "periodoInicio", "PeriodoInicio"),
            ExigirData(root, nameof(FolhaPontoFinanceiraPayload.PeriodoFim), "periodoFim", "PeriodoFim"),
            ExigirIntervalo(root, nameof(FolhaPontoFinanceiraPayload.CompetenciaAno), 1, 9999, "competenciaAno", "CompetenciaAno"),
            ExigirIntervalo(root, nameof(FolhaPontoFinanceiraPayload.CompetenciaMes), 1, 12, "competenciaMes", "CompetenciaMes"),
            ExigirLong(root, nameof(FolhaPontoFinanceiraPayload.EntidadeId), "entidadeId", "EntidadeId"),
            ExigirLong(root, nameof(FolhaPontoFinanceiraPayload.ExercicioId), "exercicioId", "ExercicioId"),
            JsonDecimal(root, "totalProventos", "TotalProventos"),
            JsonDecimal(root, "totalDescontos", "TotalDescontos"),
            TryLong(root, out var usuarioId, "usuarioId", "UsuarioId") ? usuarioId : null,
            ExigirData(root, nameof(FolhaPontoFinanceiraPayload.DataEmissao), "dataEmissao", "DataEmissao"),
            JsonTexto(root, "payloadHash", "PayloadHash"),
            itens);
    }

    // ==== Integridade do payload (RC-EVO-B §5) =======================================

    /// <summary>
    /// Checksum SHA-256 (hex minúsculo) sobre a forma canônica dos campos assináveis:
    /// escalares + emissão + itens (codigo/tipo/valor F2, ordenados ordinal). O nome da rubrica
    /// fica fora (rótulo alterável); valores de dinheiro e identidade entram sempre.
    /// Producer grava em 'payloadHash' na publicação; consumer recompara antes de agir.
    /// </summary>
    public static string CalcularHash(FolhaPontoFinanceiraPayload payload) =>
        CalcularHash(payload.IntegracaoId, payload.ApuracaoId, payload.FolhaId, payload.ServidorId,
            payload.VersaoRegras, payload.PeriodoInicio, payload.PeriodoFim, payload.CompetenciaAno,
            payload.CompetenciaMes, payload.EntidadeId, payload.ExercicioId, payload.TotalProventos,
            payload.TotalDescontos, payload.UsuarioId, payload.DataEmissao, payload.Itens);

    public static string CalcularHash(
        long integracaoId, long apuracaoId, long folhaId, long servidorId, string? versaoRegras,
        DateOnly periodoInicio, DateOnly periodoFim, int competenciaAno, int competenciaMes,
        long entidadeId, long exercicioId, decimal totalProventos, decimal totalDescontos,
        long? usuarioId, DateOnly dataEmissao, IEnumerable<ItemPayload>? itens)
    {
        static string N(long v) => v.ToString(CultureInfo.InvariantCulture);
        static string M(decimal v) => Money(v).ToString("F2", CultureInfo.InvariantCulture);
        var assinaveis = (itens ?? Array.Empty<ItemPayload>())
            .Select(i => string.Concat(i.Codigo, (char)1, i.Tipo, (char)1, M(i.Valor)))
            .OrderBy(s => s, StringComparer.Ordinal);
        var canonico = string.Join('|',
            N(integracaoId), N(apuracaoId), N(folhaId), N(servidorId), versaoRegras ?? string.Empty,
            periodoInicio.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            periodoFim.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            N(competenciaAno), N(competenciaMes), N(entidadeId), N(exercicioId),
            M(totalProventos), M(totalDescontos),
            usuarioId is long u ? N(u) : string.Empty,
            dataEmissao.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            string.Join(';', assinaveis));
        return Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(canonico))).ToLowerInvariant();
    }

    /// <summary>Falha nomeada se o payload não veio assinado ou se o checksum não bate (nunca processapayload divergente).</summary>
    public static void VerificarIntegridade(FolhaPontoFinanceiraPayload payload)
    {
        if (string.IsNullOrWhiteSpace(payload.Hash))
        {
            throw new InvalidOperationException($"{FalhaHashAusente}: o payload publicado não registra o checksum ('payloadHash') da origem; republique a integração para consumir.");
        }
        var esperado = CalcularHash(payload);
        if (!string.Equals(esperado, payload.Hash.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"{FalhaChecksumDivergente}: checksum recalculado no consumo ({esperado}) difere do gravado na publicação ({payload.Hash.Trim()}); payload pode ter sido alterado entre publicar e consumir — nenhum documento foi registrado.");
        }
    }

    // ==== Plano do empenho ==========================================================

    public sealed record EmpenhoItemPlana(string Descricao, decimal Quantidade, decimal ValorUnitario);

    public sealed record EmpenhoPlano(
        long OrcamentoDespesaId,
        long FornecedorPessoaId,
        string Historico,
        string Observacoes,
        IReadOnlyList<EmpenhoItemPlana> Itens,
        decimal ValorTotal);

    /// <summary>
    /// Monta o plano do empenho a partir do payload e das regras: apenas proventos com valor
    /// positivo viram itens (quantidade 1); sem proventos → falha nomeada SEM_PROVENTOS_PARA_EMPENHO
    /// (sem valor zero fictício). Descontos não entram no documento — apenas nas observações.
    /// </summary>
    public static EmpenhoPlano ConstruirEmpenho(FolhaPontoFinanceiraPayload payload, long orcamentoDespesaId, long fornecedorPessoaId)
    {
        var falhaRegras = ValidarRegras(orcamentoDespesaId, fornecedorPessoaId);
        if (falhaRegras is not null) throw new InvalidOperationException(falhaRegras);

        var proventos = payload.Itens
            .Where(i => string.Equals(i.Tipo, RubricaProvento, StringComparison.OrdinalIgnoreCase) && i.Valor > 0m)
            .ToList();
        if (proventos.Count == 0)
        {
            throw new InvalidOperationException($"{FalhaSemProventos}: a integração não possui lançamentos de provento com valor positivo; nada a empenhar (nenhum valor fictício é registrado).");
        }

        var competencia = $"{payload.CompetenciaMes.ToString(CultureInfo.InvariantCulture)}/{payload.CompetenciaAno.ToString(CultureInfo.InvariantCulture)}";
        var itens = proventos
            .Select(i => new EmpenhoItemPlana($"Ponto: {i.Codigo} {i.Nome} ({competencia})", 1m, Money(i.Valor)))
            .ToList();
        var total = Money(proventos.Sum(i => i.Valor));
        var historico = $"Integração de ponto — competência {competencia} — servidor {payload.ServidorId.ToString(CultureInfo.InvariantCulture)} — apuração {payload.ApuracaoId.ToString(CultureInfo.InvariantCulture)} — integração {payload.IntegracaoId.ToString(CultureInfo.InvariantCulture)} — versão de regras {(payload.VersaoRegras.Length > 0 ? payload.VersaoRegras : "sem versão")}";
        var observacoes = $"Folha de ponto (apuração {payload.ApuracaoId.ToString(CultureInfo.InvariantCulture)}): proventos {Money(payload.TotalProventos):F2}; descontos {Money(payload.TotalDescontos):F2}; líquido {Money(payload.TotalProventos - payload.TotalDescontos):F2}. Empenho sem liquidação nem pagamento automático.";
        return new EmpenhoPlano(orcamentoDespesaId, fornecedorPessoaId, historico, observacoes, itens, total);
    }

    // ==== Helpers JSON (case-insensitive) ============================================

    private static bool TryProp(JsonElement obj, IEnumerable<string> nomes, out JsonElement el)
    {
        foreach (var nome in nomes)
        {
            if (obj.TryGetProperty(nome, out el)) return true;
        }
        el = default;
        return false;
    }

    private static bool TryGetArray(JsonElement obj, out JsonElement array)
    {
        foreach (var nome in new[] { "itens", "Itens" })
        {
            if (obj.TryGetProperty(nome, out array)) return array.ValueKind == JsonValueKind.Array;
        }
        array = default;
        return false;
    }

    private static bool TryLong(JsonElement obj, out long valor, params string[] nomes)
    {
        valor = 0;
        if (!TryProp(obj, nomes, out var el)) return false;
        return el.ValueKind switch
        {
            JsonValueKind.Number => el.TryGetInt64(out valor),
            JsonValueKind.String => long.TryParse(el.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out valor),
            _ => false
        };
    }

    private static bool TryInt(JsonElement obj, out int valor, params string[] nomes)
    {
        valor = 0;
        if (!TryProp(obj, nomes, out var el)) return false;
        return el.ValueKind switch
        {
            JsonValueKind.Number => el.TryGetInt32(out valor),
            JsonValueKind.String => int.TryParse(el.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out valor),
            _ => false
        };
    }

    private static bool TryDate(JsonElement obj, out DateOnly valor, params string[] nomes)
    {
        valor = default;
        if (!TryProp(obj, nomes, out var el)) return false;
        var texto = el.ValueKind == JsonValueKind.String ? el.GetString() : el.GetRawText();
        return DateOnly.TryParseExact(texto, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out valor);
    }

    private static long ExigirLong(JsonElement obj, string campo, params string[] nomes)
    {
        if (!TryLong(obj, out var valor, nomes) || valor <= 0)
            throw new InvalidOperationException($"{FalhaPayloadInvalido}: campo '{campo}' ausente ou não positivo.");
        return valor;
    }

    private static int ExigirIntervalo(JsonElement obj, string campo, int minimo, int maximo, params string[] nomes)
    {
        if (!TryInt(obj, out var valor, nomes) || valor < minimo || valor > maximo)
            throw new InvalidOperationException($"{FalhaPayloadInvalido}: campo '{campo}' ausente ou fora do intervalo {minimo.ToString(CultureInfo.InvariantCulture)}..{maximo.ToString(CultureInfo.InvariantCulture)}.");
        return valor;
    }

    private static DateOnly ExigirData(JsonElement obj, string campo, params string[] nomes)
    {
        if (!TryDate(obj, out var data, nomes))
            throw new InvalidOperationException($"{FalhaPayloadInvalido}: campo '{campo}' ausente ou fora do formato yyyy-MM-dd.");
        return data;
    }

    private static decimal JsonDecimal(JsonElement obj, params string[] nomes)
    {
        if (!TryProp(obj, nomes, out var el)) return 0m;
        return el.ValueKind switch
        {
            JsonValueKind.Number => el.GetDecimal(),
            JsonValueKind.String => decimal.TryParse(el.GetString(), NumberStyles.Number, CultureInfo.InvariantCulture, out var v) ? v : 0m,
            _ => 0m
        };
    }

    private static string JsonTexto(JsonElement obj, params string[] nomes)
    {
        if (!TryProp(obj, nomes, out var el)) return string.Empty;
        return el.ValueKind switch
        {
            JsonValueKind.String => el.GetString() ?? string.Empty,
            JsonValueKind.Null => string.Empty,
            _ => el.GetRawText()
        };
    }
}
