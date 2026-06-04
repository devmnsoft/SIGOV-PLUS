using Dapper;
using Microsoft.Extensions.Logging;
using Sigov.Application.Abstractions;
using Sigov.Infrastructure.Persistence.Dapper;
using Sigov.Infrastructure.Persistence.Repositories;

namespace Sigov.Infrastructure.Security;

public sealed class PermissionService : BaseRepository, IPermissionService
{
    private readonly DapperContext _context;
    private readonly ILogger<PermissionService> _logger;
    private readonly ICurrentTenant _currentTenant;

    public PermissionService(DapperContext context, ILogger<PermissionService> logger, ICurrentTenant currentTenant)
    {
        _context = context;
        _logger = logger;
        _currentTenant = currentTenant;
    }

    public async Task<bool> HasPermissionAsync(long usuarioId, string modulo, string recurso, string acao, CancellationToken cancellationToken = default)
    {
        if (!_currentTenant.TenantId.HasValue)
        {
            return false;
        }

        try
        {
            const string sql = """
                select exists (
                    select 1
                    from sigov.usuario u
                    where u.id = @UsuarioId
                      and u.tenant_id = @TenantId
                      and u.ativo = true
                      and u.is_deleted = false
                      and (
                          u.login = 'admin'
                          or exists (
                              select 1
                              from sigov.usuario_grupo ug
                              join sigov.grupo_perfil gp on gp.grupo_acesso_id = ug.grupo_acesso_id and gp.is_deleted = false
                              join sigov.perfil_permissao pp on pp.perfil_acesso_id = gp.perfil_acesso_id
                              join sigov.permissao p on p.id = pp.permissao_id and p.ativo = true and p.is_deleted = false
                              where ug.usuario_id = u.id
                                and ug.tenant_id = @TenantId
                                and ug.is_deleted = false
                                and p.modulo = @Modulo
                                and (p.chave = @Chave or p.chave = @AdminChave or (p.recurso = @Recurso and p.acao = @Acao))
                          )
                      )
                );
                """;

            using var connection = _context.CreateConnection();
            return await connection.ExecuteScalarAsync<bool>(Command(sql, new { UsuarioId = usuarioId, TenantId = _currentTenant.TenantId.Value, Modulo = modulo, Recurso = recurso, Acao = acao, Chave = $"{recurso}.{acao}", AdminChave = $"{modulo}_admin" }, cancellationToken)).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao verificar permissão no schema sigov. TenantId={TenantId} UsuarioId={UsuarioId} Modulo={Modulo} Recurso={Recurso} Acao={Acao}", _currentTenant.TenantId, usuarioId, modulo, recurso, acao);
            throw;
        }
    }
}
