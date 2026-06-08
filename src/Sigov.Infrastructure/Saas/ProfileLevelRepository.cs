using Dapper;
using Sigov.Application.Saas.Profiles;
using Sigov.Infrastructure.Persistence.Dapper;

namespace Sigov.Infrastructure.Saas;

public sealed class ProfileLevelRepository : IProfileLevelRepository
{
    private readonly DapperContext _context;

    public ProfileLevelRepository(DapperContext context) => _context = context;

    public async Task<IReadOnlyCollection<ProfileLevelItem>> GetLevelsAsync(CancellationToken cancellationToken)
    {
        const string sql = @"select codigo as Codigo, nome as Nome, descricao as Descricao, nivel_hierarquico as NivelHierarquico, global as Global, tenant_admin as TenantAdmin, ativo as Ativo
from sigov.perfil_nivel
where ativo = true
order by nivel_hierarquico desc, nome;
";
        using var connection = _context.CreateConnection();
        var rows = await connection.QueryAsync<ProfileLevelItem>(new CommandDefinition(sql, cancellationToken: cancellationToken)).ConfigureAwait(false);
        return rows.AsList();
    }

    public async Task<IReadOnlyCollection<string>> GetUserProfileCodesAsync(long usuarioId, CancellationToken cancellationToken)
    {
        const string sql = @"select distinct coalesce(pn.codigo, pa.codigo_externo, upper(replace(pa.nome, ' ', '_'))) as codigo
from sigov.usuario u
left join sigov.usuario_grupo ug on ug.usuario_id = u.id and ug.is_deleted = false
left join sigov.grupo_perfil gp on gp.grupo_acesso_id = ug.grupo_acesso_id and gp.is_deleted = false
left join sigov.perfil_acesso pa on pa.id = gp.perfil_acesso_id and pa.is_deleted = false and pa.ativo = true
left join sigov.perfil_nivel pn on pn.codigo = coalesce(pa.codigo_externo, upper(replace(pa.nome, ' ', '_')))
where u.id = @UsuarioId and u.is_deleted = false and u.ativo = true
union
select case when tipo_usuario in ('SIGOV_ADMIN','SUPER_ADMIN','ADMINISTRADOR_GERAL') then 'ADMINISTRADOR_GERAL' else tipo_usuario end
from sigov.usuario
where id = @UsuarioId and tipo_usuario is not null;
";
        using var connection = _context.CreateConnection();
        var rows = await connection.QueryAsync<string>(new CommandDefinition(sql, new { UsuarioId = usuarioId }, cancellationToken: cancellationToken)).ConfigureAwait(false);
        return rows.Where(row => !string.IsNullOrWhiteSpace(row)).ToArray();
    }

    public async Task<IReadOnlyCollection<string>> GetUserPermissionsAsync(long usuarioId, long? tenantId, CancellationToken cancellationToken)
    {
        const string sql = @"select distinct pe.modulo || '.' || pe.recurso || '.' || pe.acao
from sigov.usuario u
join sigov.usuario_grupo ug on ug.usuario_id = u.id and ug.is_deleted = false
join sigov.grupo_perfil gp on gp.grupo_acesso_id = ug.grupo_acesso_id and gp.is_deleted = false
join sigov.perfil_permissao pp on pp.perfil_acesso_id = gp.perfil_acesso_id
join sigov.permissao pe on pe.id = pp.permissao_id and pe.ativo = true and pe.is_deleted = false
where u.id = @UsuarioId
  and u.ativo = true
  and u.is_deleted = false
  and (@TenantId is null or u.tenant_id = @TenantId);
";
        using var connection = _context.CreateConnection();
        var rows = await connection.QueryAsync<string>(new CommandDefinition(sql, new { UsuarioId = usuarioId, TenantId = tenantId }, cancellationToken: cancellationToken)).ConfigureAwait(false);
        return rows.AsList();
    }

    public async Task<IReadOnlyCollection<UserAccessScope>> GetUserScopesAsync(long usuarioId, long? tenantId, CancellationToken cancellationToken)
    {
        const string sql = @"select tenant_id as TenantId, entidade_id as EntidadeId, exercicio_id as ExercicioId, modulo_codigo as ModuloCodigo, escopo as Escopo
from sigov.usuario_escopo_acesso
where usuario_id = @UsuarioId
  and ativo = true
  and (@TenantId is null or tenant_id = @TenantId)
order by escopo;
";
        using var connection = _context.CreateConnection();
        var rows = await connection.QueryAsync<UserAccessScope>(new CommandDefinition(sql, new { UsuarioId = usuarioId, TenantId = tenantId }, cancellationToken: cancellationToken)).ConfigureAwait(false);
        return rows.AsList();
    }
}
