namespace Sigov.Application.Educacao.Avancada;

/// <summary>
/// Máquina de estados dos recursos avançados de educação (Ciclo 1, item #4). A matriz é definida por
/// FAMÍLIA de status (multi-recurso) e parametrizável por nomenclatura de domínio (regra 24): nenhum
/// comportamento de órgão único é hard-coded; apenas os estados canônicos do domínio público de educação.
///
/// Famílias (comparadas em maiúsculas, sem espaços nas pontas):
///   PENDENTE : RASCUNHO, PROGRAMADA, SOLICITADA
///   ATIVO    : ATIVA, ATIVO, PUBLICADO, DISPONIVEL, ABERTO
///   ALERTA   : ATRASADO, SUSPENSA, COM_INCONSISTENCIA
///   TERMINAL : ENCERRADA, ENCERRADO, CANCELADA, CANCELADO
///
/// Regras:
///   * transição para o MESMO status é sempre permitida (no-op idempotente, auditado);
///   * PENDENTE -> ATIVO | PENDENTE | CANCELADA/CANCELADO (pendente não encerra direto nem atrasa);
///   * ATIVO    -> ATIVO | ALERTA | TERMINAL;
///   * ALERTA   -> ATIVO | ALERTA | TERMINAL;
///   * TERMINAL -> nenhuma transição entre estados distintos (estado final);
///   * origem DESCONHECIDA (fora das famílias): comportamento legado preservado (permitido) — exceção
///     declarada e explícita sobre dados existentes, não regra de negócio (regra 13).
/// </summary>
public static class EducacaoStatusMaquina
{
    public static readonly IReadOnlySet<string> Pendentes = new HashSet<string>(StringComparer.Ordinal) { "RASCUNHO", "PROGRAMADA", "SOLICITADA" };
    public static readonly IReadOnlySet<string> Ativos = new HashSet<string>(StringComparer.Ordinal) { "ATIVA", "ATIVO", "PUBLICADO", "DISPONIVEL", "ABERTO" };
    public static readonly IReadOnlySet<string> Alertas = new HashSet<string>(StringComparer.Ordinal) { "ATRASADO", "SUSPENSA", "COM_INCONSISTENCIA" };
    public static readonly IReadOnlySet<string> Terminais = new HashSet<string>(StringComparer.Ordinal) { "ENCERRADA", "ENCERRADO", "CANCELADA", "CANCELADO" };

    /// <summary>Família do status (maiúsculas/trim) ou null quando desconhecida (legado).</summary>
    public static string? Grupo(string? status)
    {
        var s = (status ?? string.Empty).Trim().ToUpperInvariant();
        return Pendentes.Contains(s) ? "PENDENTE"
             : Ativos.Contains(s) ? "ATIVO"
             : Alertas.Contains(s) ? "ALERTA"
             : Terminais.Contains(s) ? "TERMINAL"
             : null;
    }

    public static bool TransicaoPermitida(string? de, string para)
    {
        if (string.IsNullOrWhiteSpace(para)) throw new ArgumentException("Status de destino é obrigatório.", nameof(para));
        var p = para.Trim().ToUpperInvariant();
        var d = (de ?? string.Empty).Trim().ToUpperInvariant();
        if (d == p) return true; // no-op idempotente (auditado na escrita)
        return Grupo(d) switch
        {
            "PENDENTE" => Ativos.Contains(p) || Pendentes.Contains(p) || p is "CANCELADA" or "CANCELADO",
            "ATIVO" => Ativos.Contains(p) || Alertas.Contains(p) || Terminais.Contains(p),
            "ALERTA" => Ativos.Contains(p) || Alertas.Contains(p) || Terminais.Contains(p),
            "TERMINAL" => false,
            _ => true // origem desconhecida: preserva comportamento legado (exceção documentada)
        };
    }
}

/// <summary>Transição não prevista pela máquina de estados (HTTP 409, código TRANSICAO_INVALIDA).</summary>
public sealed class TransicaoDeStatusInvalidaException(string De, string Para)
    : Exception($"TRANSICAO_INVALIDA: transição de status '{De}' -> '{Para}' não é permitida pela máquina de estados do recurso.");

/// <summary>Guarda de versão otimista falhou: outra operação gravou antes (HTTP 409, código CONFLITO_VERSAO).</summary>
public sealed class ConflitoDeVersaoEducacaoException(string Recurso, long Id)
    : Exception($"CONFLITO_VERSAO: registro sigov.{Recurso}/{Id} foi modificado por outra operação entre a leitura e a atualização; reler o recurso e tentar novamente.");

/// <summary>Registro inexistente para o tenant atual ou excluído logicamente (HTTP 404, código REGISTRO_NAO_ENCONTRADO).</summary>
public sealed class RegistroNaoEncontradoException(string Recurso, long Id)
    : Exception($"REGISTRO_NAO_ENCONTRADO: registro {Id} não encontrado em sigov.{Recurso} para o tenant atual (ou excluído logicamente).");

/// <summary>Tabela do recurso ausente na instância — falha operacional explícita, nunca sucesso simulado (HTTP 404, código RECURSO_INDISPONIVEL; regra 13).</summary>
public sealed class RecursoNaoDisponivelException(string Recurso)
    : Exception($"RECURSO_INDISPONIVEL: 'sigov.{Recurso}' indisponível nesta instância: schema ausente (instalação/migration pendente), não ausência de dados.");
