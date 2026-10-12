namespace Sigov.Application.Educacao.Avancada;

/// <summary>
/// Máquina de estados dos recursos avançados de educação (Ciclo 1, item #4; Ciclo 2 Sprint 2 — BR-2.1/2.4).
/// A matriz é definida por FAMÍLIA de status (multi-recurso) e parametrizável por nomenclatura de domínio
/// (regra 24): nenhum comportamento de órgão único é hard-coded; apenas os estados canônicos do domínio
/// público de educação.
///
/// Famílias (comparadas em maiúsculas, sem espaços nas pontas):
///   PENDENTE : RASCUNHO, PROGRAMADA, SOLICITADA
///   ATIVO    : ATIVA, ATIVO, PUBLICADO, DISPONIVEL, ABERTO
///   ALERTA   : ATRASADO, SUSPENSA, COM_INCONSISTENCIA
///   TERMINAL : ENCERRADA, ENCERRADO, CANCELADA, CANCELADO, CONSUMIDA
///
/// Regras:
///   * transição para o MESMO status é sempre permitida (no-op idempotente, auditado);
///   * PENDENTE -> ATIVO | PENDENTE | CANCELADA/CANCELADO (pendente não encerra direto nem atrasa);
///   * ATIVO    -> ATIVO | ALERTA | TERMINAL;
///   * ALERTA   -> ATIVO | ALERTA | TERMINAL;
///   * TERMINAL -> nenhuma transição entre estados distintos (BR-2.4: estado terminal é irreversível;
///     a reabertura somente existe quando a família declara porta própria — hoje nenhuma família
///     reabre via máquina genérica);
///   * origem FORA das famílias vira o estado LEGADO_NAO_REGULARIZADO (visível, tratável, não
///     silenciosa — BR-2.1): a transição é permitida apenas como REGULARIZAÇÃO para uma família
///     canônica (sem salto direto a TERMINAL e sem destino não canônico), exige justificativa
///     (REGULARIZACAO_LEGADA_EXIGE_JUSTIFICATIVA) e fica visível na auditoria pelo campo 'de'.
///     O wildcard legado '_ =&gt; true' foi eliminado.
/// </summary>
public static class EducacaoStatusMaquina
{
    public static readonly IReadOnlySet<string> Pendentes = new HashSet<string>(StringComparer.Ordinal) { "RASCUNHO", "PROGRAMADA", "SOLICITADA" };
    public static readonly IReadOnlySet<string> Ativos = new HashSet<string>(StringComparer.Ordinal) { "ATIVA", "ATIVO", "PUBLICADO", "DISPONIVEL", "ABERTO" };
    public static readonly IReadOnlySet<string> Alertas = new HashSet<string>(StringComparer.Ordinal) { "ATRASADO", "SUSPENSA", "COM_INCONSISTENCIA" };
    public static readonly IReadOnlySet<string> Terminais = new HashSet<string>(StringComparer.Ordinal) { "ENCERRADA", "ENCERRADO", "CANCELADA", "CANCELADO", "CONSUMIDA" }; // CONSUMIDA (reserva consumida na devolução; BR-3.2)

    /// <summary>Estado virtual dos registros cujo status está fora das famílias canônicas (BR-2.1).</summary>
    public const string LegadoNaoRegularizado = "LEGADO_NAO_REGULARIZADO";

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

    /// <summary>Família efetiva usada pela máquina: origem desconhecida é classificada como LEGADO_NAO_REGULARIZADO.</summary>
    public static string GrupoEfetivo(string? status) => Grupo(status) ?? LegadoNaoRegularizado;

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
            // LEGADO_NAO_REGULARIZADO: só regulariza para família canônica (BR-2.1 — wildcard de origem eliminado):
            // nunca salto direto a TERMINAL, nunca destino fora das famílias.
            _ => Ativos.Contains(p) || Pendentes.Contains(p) || Alertas.Contains(p),
        };
    }
}

/// <summary>
/// Efeito declarado por recurso × estados alvo (Ciclo 2 Sprint 2 — BR-2.1): o que muda FORA do
/// registro ao entrar no estado alvo. A matriz é documental + executável: apenas o efeito que
/// materializa linhas/disponibilidade em OUTROS registros (distribuição → movimentos SAIDA sobre
/// estoque) exige compensação tipada antes da escrita genérica (<see cref="ExigeCompensacaoAntesDeGravar"/>).
/// </summary>
public sealed record EducacaoEfeitoTransicao(string Recurso, string EstadosAlvo, string EfeitoForaDoRegistro, string PortaCompensacao);

public static class EducacaoEfeitosTransicao
{
    /// <summary>Matriz família × estado × efeito (documentação executável; espelhada em MATRIZ_FLUXOS_VERTICAIS.md).</summary>
    public static readonly IReadOnlyList<EducacaoEfeitoTransicao> Matriz = new EducacaoEfeitoTransicao[]
    {
        new("educacao_merenda_distribuicao", "CANCELADA/CANCELADO/ENCERRADA/ENCERRADO", "baixa de estoque já efetivada em movimentos SAIDA (linhas em educacao_merenda_movimento/estoque)", "POST /api/educacao/merenda/distribuicoes/{id}/estornar"),
        new("educacao_biblioteca_emprestimo", "ENCERRADA/ENCERRADO/CANCELADA/CANCELADO", "exemplar liberado (índice parcial de vigência) + registro de devolução (linha em educacao_biblioteca_devolucao)", "POST /api/educacao/biblioteca/emprestimos/{id}/devolver"),
        new("educacao_biblioteca_reserva", "CANCELADA/CANCELADO", "fila do exemplar liberada (índice parcial de vigência)", "— (efeito de índice, sem linha externa)"),
        new("educacao_biblioteca_reserva", "CONSUMIDA", "empréstimo criado para o tomador reservante (linha em educacao_biblioteca_emprestimo) + fila do exemplar liberada (índice parcial de vigência)", "— (comando tipado de devolução; BR-3.2)"),
        new("educacao_fundeb_receita", "CANCELADA/CANCELADO", "libera unicidade de competência/fonte (índice parcial)", "— (efeito de índice, sem linha externa)"),
        new("educacao_custo_centro", "CANCELADA/CANCELADO", "centro deixa de ser base para novas aplicações (checagem no comando tipado)", "— (regra no comando)"),
    };

    /// <summary>Guarda executável: ao entrar em estado terminal, o efeito fora do registro exige compensação tipada ANTES de gravar (BR-2.1).</summary>
    public static bool ExigeCompensacaoAntesDeGravar(string recurso, string alvo) =>
        recurso == "educacao_merenda_distribuicao" && alvo is "CANCELADA" or "CANCELADO" or "ENCERRADA" or "ENCERRADO";
}

/// <summary>Transição não prevista pela máquina de estados (HTTP 409, código TRANSICAO_INVALIDA).</summary>
public sealed class TransicaoDeStatusInvalidaException(string De, string Para)
    : Exception($"TRANSICAO_INVALIDA: transição de status '{De}' -> '{Para}' não é permitida pela máquina de estados do recurso.");

/// <summary>
/// Regularização a partir de LEGADO_NAO_REGULARIZADO sem justificativa (HTTP 409, código
/// REGULARIZACAO_LEGADA_EXIGE_JUSTIFICATIVA): a transição sai do estado legado de forma visível e
/// auditável (BR-2.1 — não silenciosa).
/// </summary>
public sealed class RegularizacaoLegadaExigeJustificativaException(string StatusLegado)
    : Exception($"REGULARIZACAO_LEGADA_EXIGE_JUSTIFICATIVA: o status atual '{StatusLegado}' está fora das famílias canônicas da máquina (estado LEGADO_NAO_REGULARIZADO); informe justificativa para tornar a regularização visível e auditável.");

/// <summary>
/// Criação genérica restrita: recurso com invariante tipada declarada — o comando tipado é a única
/// porta de mutação (HTTP 409, código COMANDO_TIPADO_OBRIGATORIO; BR-2.2 / padrão CQRS-lite).
/// </summary>
public sealed class ComandoTipadoObrigatorioException(string Recurso, string RotaTipada)
    : Exception($"COMANDO_TIPADO_OBRIGATORIO: a criação genérica de 'sigov.{Recurso}' está restrita; use o comando tipado '{RotaTipada}' (única porta de mutação com invariante declarada).");

/// <summary>Guarda de versão otimista falhou: outra operação gravou antes (HTTP 409, código CONFLITO_VERSAO).</summary>
public sealed class ConflitoDeVersaoEducacaoException(string Recurso, long Id)
    : Exception($"CONFLITO_VERSAO: registro sigov.{Recurso}/{Id} exige a versão atual (updatedAt) após a primeira atualização ou a versão enviada está obsoleta; reler o recurso e repetir com updatedAt.");

/// <summary>Registro inexistente para o tenant atual ou excluído logicamente (HTTP 404, código REGISTRO_NAO_ENCONTRADO).</summary>
public sealed class RegistroNaoEncontradoException(string Recurso, long Id)
    : Exception($"REGISTRO_NAO_ENCONTRADO: registro {Id} não encontrado em sigov.{Recurso} para o tenant atual (ou excluído logicamente).");

/// <summary>Tabela do recurso ausente na instância — falha operacional explícita, nunca sucesso simulado (HTTP 404, código RECURSO_INDISPONIVEL; regra 13).</summary>
public sealed class RecursoNaoDisponivelException(string Recurso)
    : Exception($"RECURSO_INDISPONIVEL: 'sigov.{Recurso}' indisponível nesta instância: schema ausente (instalação/migration pendente), não ausência de dados.");
