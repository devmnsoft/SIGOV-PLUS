using Dapper;
using Microsoft.Extensions.Logging;
using Sigov.Application.Authorization;
using Sigov.Infrastructure.Persistence.Dapper;

namespace Sigov.Infrastructure.Security;

/// <summary>
/// Avaliador único e fail-closed. O nome do perfil nunca participa da decisão:
/// negativa aplicável prevalece, seguida de concessão dentro da alçada; todo o
/// restante é negado. Wildcards somente existem quando persistidos como "*".
/// </summary>
public sealed class PersistentAuthorizationEvaluator : IAuthorizationEvaluator
{
    private readonly DapperContext _context;
    private readonly ILogger<PersistentAuthorizationEvaluator> _logger;

    public PersistentAuthorizationEvaluator(DapperContext context, ILogger<PersistentAuthorizationEvaluator> logger) =>
        (_context, _logger) = (context, logger);

    public async Task<AuthorizationDecision> EvaluateAsync(AuthorizationRequest request, CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;
        var invalidReason = Validate(request);
        if (invalidReason.HasValue)
            return AuthorizationDecision.Deny(invalidReason.Value, request, now);

        try
        {
            using var connection = _context.CreateConnection();
            var result = await connection.QueryMultipleAsync(new CommandDefinition(Sql, new
            {
                request.UsuarioId,
                Modulo = request.Modulo.Trim(),
                Recurso = request.Recurso.Trim(),
                Acao = request.Acao.Trim(),
                request.TenantId,
                request.EntidadeId,
                request.ExercicioId,
                request.UnidadeId,
                request.Valor,
                Agora = now
            }, cancellationToken: cancellationToken)).ConfigureAwait(false);

            var state = await result.ReadSingleAsync<EvaluationState>().ConfigureAwait(false);
            var rules = (await result.ReadAsync<ApplicableRule>().ConfigureAwait(false)).AsList();
            AuthorizationDecision decision;

            if (!state.UsuarioValido)
                decision = AuthorizationDecision.Deny(AuthorizationDecisionReason.UsuarioInativoOuInexistente, request, now);
            else if (!state.RecursoValido)
                decision = AuthorizationDecision.Deny(AuthorizationDecisionReason.RecursoOuAcaoInexistente, request, now);
            else if (rules.FirstOrDefault(x => x.Efeito.Equals("NEGAR", StringComparison.OrdinalIgnoreCase)) is { } deny)
                decision = AuthorizationDecision.Deny(AuthorizationDecisionReason.NegativaExplicita, request, now, true, deny.AlcadaValor);
            else if (rules.FirstOrDefault(x => x.Efeito.Equals("PERMITIR", StringComparison.OrdinalIgnoreCase) &&
                         (x.AlcadaValor is null || request.Valor is null || request.Valor <= x.AlcadaValor)) is { } allow)
                decision = AuthorizationDecision.Allow(request, now, allow.PerfilId, allow.PermissaoId, allow.AlcadaValor);
            else if (request.Valor.HasValue && rules.Any(x => x.Efeito.Equals("PERMITIR", StringComparison.OrdinalIgnoreCase) && x.AlcadaValor.HasValue))
                decision = AuthorizationDecision.Deny(AuthorizationDecisionReason.AlcadaInsuficiente, request, now,
                    limit: rules.Where(x => x.Efeito.Equals("PERMITIR", StringComparison.OrdinalIgnoreCase)).Max(x => x.AlcadaValor));
            else
                decision = AuthorizationDecision.Deny(AuthorizationDecisionReason.SemConcessaoAplicavel, request, now);

            await AuditAsync(connection, request, decision, cancellationToken).ConfigureAwait(false);
            LogDecision(request, decision);
            return decision;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falha fechada no avaliador. UsuarioId={UsuarioId} TenantId={TenantId} Modulo={Modulo} Recurso={Recurso} Acao={Acao} CorrelationId={CorrelationId}",
                request.UsuarioId, request.TenantId, request.Modulo, request.Recurso, request.Acao, request.CorrelationId);
            return AuthorizationDecision.Deny(AuthorizationDecisionReason.FalhaNaAvaliacao, request, now);
        }
    }

    private static AuthorizationDecisionReason? Validate(AuthorizationRequest request)
    {
        if (request.UsuarioId <= 0 || string.IsNullOrWhiteSpace(request.Modulo) ||
            string.IsNullOrWhiteSpace(request.Recurso) || string.IsNullOrWhiteSpace(request.Acao) ||
            request.Modulo.Length > 60 || request.Recurso.Length > 160 || request.Acao.Length > 80 ||
            request.Valor < 0)
            return AuthorizationDecisionReason.RequisicaoInvalida;
        return null;
    }

    private static Task AuditAsync(System.Data.IDbConnection connection, AuthorizationRequest request, AuthorizationDecision decision, CancellationToken ct) =>
        connection.ExecuteAsync(new CommandDefinition(AuditSql, new
        {
            request.UsuarioId, request.TenantId, request.EntidadeId, request.ExercicioId, request.UnidadeId,
            request.Modulo, request.Recurso, request.Acao, request.Valor,
            Permitido = decision.Permitido, Efeito = decision.NegativaExplicita ? "NEGAR" : decision.Permitido ? "PERMITIR" : null,
            Motivo = decision.Motivo.ToString().ToUpperInvariant(), request.CorrelationId, request.Origem,
            decision.PerfilId, decision.VinculoId, decision.LimiteAlcada, decision.DecididoEmUtc
        }, cancellationToken: ct));

    private void LogDecision(AuthorizationRequest request, AuthorizationDecision decision) =>
        _logger.Log(decision.Permitido ? LogLevel.Information : LogLevel.Warning,
            "Decisão de autorização. UsuarioId={UsuarioId} TenantId={TenantId} Modulo={Modulo} Recurso={Recurso} Acao={Acao} Permitido={Permitido} Motivo={Motivo} CorrelationId={CorrelationId}",
            request.UsuarioId, request.TenantId, request.Modulo, request.Recurso, request.Acao, decision.Permitido, decision.Motivo, request.CorrelationId);

    private sealed record EvaluationState(bool UsuarioValido, bool RecursoValido);
    private sealed record ApplicableRule(long PerfilId, long PermissaoId, string Efeito, decimal? AlcadaValor);

    private const string Sql = """
select exists(select 1 from sigov.usuario u where u.id=@UsuarioId and u.ativo and not u.is_deleted) as UsuarioValido,
       exists(select 1 from sigov.permissao p where p.ativo and not p.is_deleted
          and (p.modulo=@Modulo or p.modulo='*') and (p.recurso=@Recurso or p.recurso='*') and (p.acao=@Acao or p.acao='*')) as RecursoValido;

select pa.id as PerfilId, p.id as PermissaoId, pp.efeito as Efeito, pp.alcada_valor as AlcadaValor
from sigov.usuario u
join sigov.usuario_grupo ug on ug.usuario_id=u.id and ug.ativo and not ug.is_deleted
join sigov.grupo_acesso ga on ga.id=ug.grupo_acesso_id and ga.ativo and not ga.is_deleted
join sigov.grupo_perfil gp on gp.grupo_acesso_id=ga.id and gp.ativo and not gp.is_deleted
join sigov.perfil_acesso pa on pa.id=gp.perfil_acesso_id and pa.ativo and not pa.is_deleted
join sigov.perfil_permissao pp on pp.perfil_acesso_id=pa.id and pp.ativo and not pp.is_deleted
join sigov.permissao p on p.id=pp.permissao_id and p.ativo and not p.is_deleted
where u.id=@UsuarioId and u.ativo and not u.is_deleted
 and (p.modulo=@Modulo or p.modulo='*') and (p.recurso=@Recurso or p.recurso='*') and (p.acao=@Acao or p.acao='*')
 and (ug.vigencia_inicio is null or ug.vigencia_inicio<=@Agora) and (ug.vigencia_fim is null or ug.vigencia_fim>=@Agora)
 and (gp.vigencia_inicio is null or gp.vigencia_inicio<=@Agora) and (gp.vigencia_fim is null or gp.vigencia_fim>=@Agora)
 and (pp.vigencia_inicio is null or pp.vigencia_inicio<=@Agora) and (pp.vigencia_fim is null or pp.vigencia_fim>=@Agora)
 and (ug.tenant_id is null or ug.tenant_id=@TenantId) and (gp.tenant_id is null or gp.tenant_id=@TenantId) and (pp.tenant_id is null or pp.tenant_id=@TenantId)
 and (ug.entidade_id is null or ug.entidade_id=@EntidadeId) and (gp.entidade_id is null or gp.entidade_id=@EntidadeId) and (pp.entidade_id is null or pp.entidade_id=@EntidadeId)
 and (ug.exercicio_id is null or ug.exercicio_id=@ExercicioId) and (gp.exercicio_id is null or gp.exercicio_id=@ExercicioId) and (pp.exercicio_id is null or pp.exercicio_id=@ExercicioId)
 and (ug.unidade_id is null or ug.unidade_id=@UnidadeId) and (gp.unidade_id is null or gp.unidade_id=@UnidadeId) and (pp.unidade_id is null or pp.unidade_id=@UnidadeId)
order by case when pp.efeito='NEGAR' then 0 else 1 end,
         (case when pp.tenant_id is not null then 1 else 0 end + case when pp.entidade_id is not null then 1 else 0 end +
          case when pp.exercicio_id is not null then 1 else 0 end + case when pp.unidade_id is not null then 1 else 0 end) desc,
         pp.alcada_valor desc nulls first, pa.id, p.id;
""";

    private const string AuditSql = """
insert into sigov.autorizacao_decisao_auditoria
 (usuario_id,tenant_id,entidade_id,exercicio_id,unidade_id,modulo,recurso,acao,valor_solicitado,permitido,efeito,motivo,correlation_id,origem,perfil_id,vinculo_permissao_id,limite_alcada,decidido_em_utc)
values
 (@UsuarioId,@TenantId,@EntidadeId,@ExercicioId,@UnidadeId,@Modulo,@Recurso,@Acao,@Valor,@Permitido,@Efeito,@Motivo,@CorrelationId,@Origem,@PerfilId,@VinculoId,@LimiteAlcada,@DecididoEmUtc);
""";
}
