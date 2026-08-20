using Dapper;
using Microsoft.Extensions.Logging;
using Sigov.Application.Authorization;
using Sigov.Infrastructure.Persistence.Dapper;


namespace Sigov.Infrastructure.Security;

/// <summary>Avaliador único, fail-closed, cuja única autoridade é o PostgreSQL.</summary>
public sealed class PersistentAuthorizationEvaluator : IAuthorizationEvaluator
{
    private readonly DapperContext _context;
    private readonly ILogger<PersistentAuthorizationEvaluator> _logger;
    public PersistentAuthorizationEvaluator(DapperContext context, ILogger<PersistentAuthorizationEvaluator> logger) => (_context, _logger) = (context, logger);

    public async Task<AuthorizationDecision> EvaluateAsync(AuthorizationRequest request, CancellationToken cancellationToken = default)
    {
        if (request.UsuarioId <= 0 || string.IsNullOrWhiteSpace(request.Recurso) || string.IsNullOrWhiteSpace(request.Acao))
            return Denied(request, "CONTEXTO_INCOMPLETO");
        try
        {
            using var connection = _context.CreateConnection();
            var effects = (await connection.QueryAsync<string>(new CommandDefinition(Sql, request, cancellationToken: cancellationToken)).ConfigureAwait(false)).AsList();
            if (effects.Any(x => x.Equals("NEGAR", StringComparison.OrdinalIgnoreCase))) return Denied(request, "NEGATIVA_EXPLICITA");
            return effects.Any(x => x.Equals("PERMITIR", StringComparison.OrdinalIgnoreCase)) ? AuthorizationDecision.Allow() : Denied(request, "SEM_CONCESSAO_APLICAVEL");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falha no avaliador persistente. UsuarioId={UsuarioId} TenantId={TenantId} Recurso={Recurso} Acao={Acao}", request.UsuarioId, request.TenantId, request.Recurso, request.Acao);
            throw;
        }
    }

    private AuthorizationDecision Denied(AuthorizationRequest r, string reason)
    {
        _logger.LogWarning("Acesso negado. UsuarioId={UsuarioId} TenantId={TenantId} Recurso={Recurso} Acao={Acao} EntidadeId={EntidadeId} ExercicioId={ExercicioId} UnidadeId={UnidadeId} Motivo={Motivo}", r.UsuarioId, r.TenantId, r.Recurso, r.Acao, r.EntidadeId, r.ExercicioId, r.UnidadeId, reason);
        return AuthorizationDecision.Deny(reason);
    }

    private const string Sql = """
select pp.efeito
from sigov.usuario u
join sigov.usuario_grupo ug on ug.usuario_id=u.id and not ug.is_deleted
join sigov.grupo_acesso ga on ga.id=ug.grupo_acesso_id and ga.ativo and not ga.is_deleted
join sigov.grupo_perfil gp on gp.grupo_acesso_id=ga.id and not gp.is_deleted
join sigov.perfil_acesso pa on pa.id=gp.perfil_acesso_id and pa.ativo and not pa.is_deleted
join sigov.perfil_permissao pp on pp.perfil_acesso_id=pa.id
join sigov.permissao p on p.id=pp.permissao_id and p.ativo and not p.is_deleted
where u.id=@UsuarioId and u.ativo and not u.is_deleted and p.recurso=@Recurso and p.acao=@Acao
 and (ug.vigencia_inicio is null or ug.vigencia_inicio<=now()) and (ug.vigencia_fim is null or ug.vigencia_fim>now())
 and (gp.vigencia_inicio is null or gp.vigencia_inicio<=now()) and (gp.vigencia_fim is null or gp.vigencia_fim>now())
 and (pp.vigencia_inicio is null or pp.vigencia_inicio<=now()) and (pp.vigencia_fim is null or pp.vigencia_fim>now())
 and (pp.alcada_valor is null or (@Valor is not null and @Valor<=pp.alcada_valor))
 and ((pa.codigo_externo='SUPERADMIN' and pp.tenant_id is null)
  or (@TenantId is not null
   and (ug.tenant_id is null or ug.tenant_id=@TenantId) and (gp.tenant_id is null or gp.tenant_id=@TenantId) and (pp.tenant_id is null or pp.tenant_id=@TenantId)
   and (ug.entidade_id is null or ug.entidade_id=@EntidadeId) and (gp.entidade_id is null or gp.entidade_id=@EntidadeId) and (pp.entidade_id is null or pp.entidade_id=@EntidadeId)
   and (ug.exercicio_id is null or ug.exercicio_id=@ExercicioId) and (gp.exercicio_id is null or gp.exercicio_id=@ExercicioId) and (pp.exercicio_id is null or pp.exercicio_id=@ExercicioId)
   and (ug.unidade_id is null or ug.unidade_id=@UnidadeId) and (gp.unidade_id is null or gp.unidade_id=@UnidadeId) and (pp.unidade_id is null or pp.unidade_id=@UnidadeId)));
""";
}
