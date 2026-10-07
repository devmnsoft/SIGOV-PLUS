using System.Globalization;
using System.Text.Json;

namespace Sigov.Domain.Rh;

/// <summary>
/// RC-EVO-RH §7: regras puras da integração da apuração de ponto homologada na folha.
/// O backend é a autoridade única: status da folha de destino, críticas segundo parâmetros de
/// módulo e o catálogo RUBRICAS_PONTO são interpretados fail-closed — ausente ou inválido gera
/// falha nomeada; nunca há valor fictício ou padrão inventado.
/// </summary>
public static class FolhaRegras
{
    // ==== Falhas nomeadas =======================================================

    public const string FalhaFolhaFechada = "FOLHA_FECHADA";
    public const string FalhaFolhaCancelada = "FOLHA_CANCELADA";
    public const string FalhaFolhaStatusInvalido = "FOLHA_STATUS_INVALIDO";
    public const string RubricasAusentes = "RUBRICAS_AUSENTES";
    public const string RubricasInvalidas = "RUBRICAS_INVALIDAS";

    // ==== Status da folha de destino ==================================================

    private static readonly HashSet<string> StatusFolhaAceitos = new(StringComparer.OrdinalIgnoreCase)
    {
        "ABERTA", "CALCULADA"
    };

    /// <summary>Valida o status da folha de destino: null quando aceita lançamentos; senão falha nomeada com explicação.</summary>
    public static string? ValidarStatusFolha(string? status)
    {
        var normalizado = status?.Trim() ?? string.Empty;
        if (StatusFolhaAceitos.Contains(normalizado)) return null;
        if (string.Equals(normalizado, "FECHADA", StringComparison.OrdinalIgnoreCase))
            return $"{FalhaFolhaFechada}: a folha está FECHADA; reabra a folha ou integre em outra competência antes de lançar o ponto.";
        if (string.Equals(normalizado, "CANCELADA", StringComparison.OrdinalIgnoreCase))
            return $"{FalhaFolhaCancelada}: a folha de destino foi cancelada; selecione outra folha.";
        return $"{FalhaFolhaStatusInvalido}: a folha está com status '{(normalizado.Length > 0 ? normalizado : "ausente")}'; apenas ABERTA ou CALCULADA recebem lançamentos de ponto.";
    }

    // ==== Linha de integração (índice único de origem: tenant + apuracaoId + evento) ===

    public const string IntegracaoProcessada = "PROCESSADA";
    public const string IntegracaoPendente = "PENDENTE";
    public const string IntegracaoFalha = "FALHA";
    public const string IntegracaoCancelada = "CANCELADA";

    /// <summary>Identificador do evento de integração gravado junto à origem na apuração.</summary>
    public const string EventoIntegracaoFolha = "INTEGRACAO_FOLHA";

    /// <summary>Somente status sem efeito materializado (ou defasado) são reaproveitáveis; status desconhecido bloqueia por falha nomeada.</summary>
    public static bool StatusIntegracaoReutilizavel(string? status)
    {
        var normalizado = status?.Trim().ToUpperInvariant();
        return normalizado is IntegracaoProcessada or IntegracaoPendente or IntegracaoFalha or IntegracaoCancelada;
    }

    /// <summary>Critica bloqueia a integração apenas quando há pendências E o parâmetro habilita o bloqueio E não permite crítica não bloqueante.</summary>
    public static bool CriticaBloqueia(bool bloquearComCritica, bool permitirNaoBloqueante, IReadOnlyCollection<string>? criticas)
        => criticas is { Count: > 0 } && bloquearComCritica && !permitirNaoBloqueante;

    // ==== Rubricas de lançamento (parâmetro RUBRICAS_PONTO, módulo FOLHA) =====================

    public const string RubricaProvento = "PROVENTO";
    public const string RubricaDesconto = "DESCONTO";
    public const string BaseMinutosTrabalhados = "MINUTOS_TRABALHADOS";
    public const string BaseMinutosIntervalo = "MINUTOS_INTERVALO";
    public const string BaseDiasFalta = "DIAS_FALTA";

    private static readonly HashSet<string> TiposRubrica = new(StringComparer.OrdinalIgnoreCase) { RubricaProvento, RubricaDesconto };
    private static readonly HashSet<string> BasesRubrica = new(StringComparer.OrdinalIgnoreCase) { BaseMinutosTrabalhados, BaseMinutosIntervalo, BaseDiasFalta };

    /// <summary>Rubrica de lançamento da apuração: valor = taxa × quantidade da base (minutos convertidos ao ritmo de hora).</summary>
    public sealed record RubricaPonto(string Codigo, string Nome, string Tipo, string Base, decimal Taxa)
    {
        private static readonly HashSet<string> BasesEmMinutos = new(StringComparer.OrdinalIgnoreCase) { BaseMinutosTrabalhados, BaseMinutosIntervalo };

        /// <summary>True quando a base está em minutos (valor convertido ao ritmo de hora: ÷60).</summary>
        public bool EmMinutos => BasesEmMinutos.Contains(Base);
    }

    /// <summary>
    /// Interpreta o catálogo RUBRICAS_PONTO: array JSON de {codigo, nome, tipo, base, taxa}; os nomes
    /// das propriedades são case-insensitive, taxa aceita número ou texto numérico e tipo/base são
    /// normalizados para maiúsculas. Fail-closed: ausente/vazio → <see cref="RubricasAusentes"/>;
    /// malformado → <see cref="RubricasInvalidas"/> apontando o índice do item problemático.
    /// </summary>
    public static (IReadOnlyList<RubricaPonto> Rubricas, string? Falha) InterpretarRubricasPonto(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return (Array.Empty<RubricaPonto>(), $"{RubricasAusentes}: o parâmetro RUBRICAS_PONTO (módulo FOLHA) não possui rubricas cadastradas; cadastre o catálogo antes de integrar.");

        JsonElement raiz;
        try
        {
            raiz = JsonSerializer.Deserialize<JsonElement>(json);
        }
        catch (JsonException)
        {
            return (Array.Empty<RubricaPonto>(), $"{RubricasInvalidas}: o catálogo RUBRICAS_PONTO não é um documento JSON válido.");
        }
        if (raiz.ValueKind != JsonValueKind.Array)
            return (Array.Empty<RubricaPonto>(), $"{RubricasInvalidas}: o catálogo RUBRICAS_PONTO deve ser um array JSON de rubricas.");
        if (raiz.GetArrayLength() == 0)
            return (Array.Empty<RubricaPonto>(), $"{RubricasAusentes}: o parâmetro RUBRICAS_PONTO está vazio; cadastre ao menos uma rubrica antes de integrar.");

        var lista = new List<RubricaPonto>(raiz.GetArrayLength());
        var codigosVistos = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var indice = 0;
        foreach (var item in raiz.EnumerateArray())
        {
            if (InterpretarRubrica(item, indice, codigosVistos, out var rubrica) is { } falha)
                return (Array.Empty<RubricaPonto>(), falha);
            lista.Add(rubrica!);
            codigosVistos.Add(rubrica!.Codigo);
            indice++;
        }
        return (lista, null);
    }

    private static string? InterpretarRubrica(JsonElement item, int indice, HashSet<string> codigosVistos, out RubricaPonto? rubrica)
    {
        rubrica = null;
        if (item.ValueKind != JsonValueKind.Object)
            return $"{RubricasInvalidas}: a rubrica [{indice}] do catálogo não é um objeto JSON.";

        var props = new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);
        foreach (var prop in item.EnumerateObject()) props[prop.Name] = prop.Value;
        string? Texto(string chave)
        {
            if (!props.TryGetValue(chave, out var v) || v.ValueKind != JsonValueKind.String) return null;
            var texto = v.GetString();
            return texto is not null && !string.IsNullOrWhiteSpace(texto) ? texto.Trim() : null;
        }

        var codigo = Texto("codigo");
        if (codigo is null) return $"{RubricasInvalidas}: a rubrica [{indice}] do catálogo não possui 'codigo'.";
        var nome = Texto("nome");
        if (nome is null) return $"{RubricasInvalidas}: a rubrica [{indice}] do catálogo não possui 'nome'.";

        var tipoBruto = Texto("tipo");
        var tipo = tipoBruto?.ToUpperInvariant();
        if (tipo is null || !TiposRubrica.Contains(tipo))
            return $"{RubricasInvalidas}: a rubrica [{indice}] do catálogo possui tipo inválido '{tipoBruto ?? "ausente"}' (esperado PROVENTO ou DESCONTO).";

        var baseBruta = Texto("base");
        var baseNormalizada = baseBruta?.ToUpperInvariant();
        if (baseNormalizada is null || !BasesRubrica.Contains(baseNormalizada))
            return $"{RubricasInvalidas}: a rubrica [{indice}] do catálogo possui base inválida '{baseBruta ?? "ausente"}' (esperado MINUTOS_TRABALHADOS, MINUTOS_INTERVALO ou DIAS_FALTA).";

        if (!codigosVistos.Add(codigo))
            return $"{RubricasInvalidas}: a rubrica [{indice}] do catálogo repete o código '{codigo}' já presente no catálogo.";

        if (!props.TryGetValue("taxa", out var taxaEl))
            return $"{RubricasInvalidas}: a rubrica [{indice}] do catálogo não possui 'taxa'.";
        decimal taxa;
        if (taxaEl.ValueKind == JsonValueKind.Number && taxaEl.TryGetDecimal(out var taxaNumero) && taxaNumero >= 0m)
        {
            taxa = taxaNumero;
        }
        else if (taxaEl.ValueKind == JsonValueKind.String && decimal.TryParse(taxaEl.GetString(), NumberStyles.Number, CultureInfo.InvariantCulture, out var taxaTexto) && taxaTexto >= 0m)
        {
            taxa = taxaTexto;
        }
        else
        {
            return $"{RubricasInvalidas}: a rubrica [{indice}] do catálogo possui taxa inválida '{taxaEl.GetRawText()}' (esperado número maior ou igual a zero).";
        }

        rubrica = new RubricaPonto(codigo, nome, tipo, baseNormalizada, taxa);
        return null;
    }

    /// <summary>Quantidade da base que alimenta a rubrica (nunca negativa).</summary>
    public static int QuantidadeBase(RubricaPonto rubrica, int minutosTrabalhados, int minutosIntervalo, int diasFalta)
    {
        var valor = rubrica.Base switch
        {
            BaseMinutosTrabalhados => minutosTrabalhados,
            BaseMinutosIntervalo => minutosIntervalo,
            BaseDiasFalta => diasFalta,
            _ => 0
        };
        return Math.Max(0, valor);
    }

    /// <summary>Valor a lançar: bases em minutos convertem ao ritmo de hora (÷60); DIAS_FALTA aplica a taxa direto; sempre 2 casas (AwayFromZero).</summary>
    public static decimal ValorLancamento(RubricaPonto rubrica, int quantidadeBase)
    {
        if (quantidadeBase <= 0 || rubrica.Taxa <= 0m) return 0m;
        var bruto = rubrica.EmMinutos ? rubrica.Taxa * quantidadeBase / 60m : rubrica.Taxa * quantidadeBase;
        return Math.Round(bruto, 2, MidpointRounding.AwayFromZero);
    }
}
