namespace Sigov.Application.Educacao.Avancada;

// Falhas nomeadas de escopo institucional (Ciclo 1, item #6): escola/ano letivo/contexto
// devem pertencer ao mesmo tenant e à mesma entidade do contexto autenticado antes de
// qualquer vínculo ser gravado. Mensagem segue o padrão de código nomeado do módulo
// (regra 13: falha explícita; regra 24: nada municipal hardcoded — a entidade é o escopo).

/// <summary>Escola inexistente para o tenant atual ou excluída logicamente (HTTP 404, código ESCOLA_INEXISTENTE).</summary>
public sealed class EscolaInexistenteException(long EscolaId)
    : Exception($"ESCOLA_INEXISTENTE: escola {EscolaId} não encontrada em sigov.escola para o tenant atual (ou excluída logicamente).");

/// <summary>Escola de outra entidade do contexto institucional (HTTP 409, código ESCOLA_FORA_DO_ESCOPO).</summary>
public sealed class EscolaForaDoEscopoException(long EscolaId, long EscolaEntidadeId, long ContextoEntidadeId)
    : Exception($"ESCOLA_FORA_DO_ESCOPO: escola {EscolaId} pertence à entidade {EscolaEntidadeId} e o comando está no contexto da entidade {ContextoEntidadeId}; vínculos não podem cruzar entidades.");

/// <summary>Ano letivo ausente para o tenant/entidade (e escola quando informada) (HTTP 422, código ANO_LETIVO_INEXISTENTE).</summary>
public sealed class AnoLetivoInexistenteException(int AnoLetivo)
    : Exception($"ANO_LETIVO_INEXISTENTE: não existe ano letivo {AnoLetivo} em sigov.ano_letivo para o tenant/entidade do contexto; cadastre o ano letivo ou corrija o parâmetro.");

/// <summary>Registro de outra entidade do contexto institucional (HTTP 409, código CONTEXTO_INSTITUCIONAL_DIVERGENTE).</summary>
public sealed class ContextoInstitucionalDivergenteException(string Recurso, long Id, long LinhaEntidadeId, long ContextoEntidadeId)
    : Exception($"CONTEXTO_INSTITUCIONAL_DIVERGENTE: registro sigov.{Recurso}/{Id} pertence à entidade {LinhaEntidadeId} e o comando está no contexto da entidade {ContextoEntidadeId}; troque de contexto institucional antes de operar.");
