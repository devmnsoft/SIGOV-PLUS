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

    public PermissionService(DapperContext context, ILogger<PermissionService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<bool> HasPermissionAsync(long usuarioId, string modulo, string recurso, string acao, CancellationToken cancellationToken = default)
    {
        try
        {
            const string sql = """
                select exists (
                    select 1
                    from sigov.usuario u
                    where u.id = @UsuarioId
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
                                and ug.is_deleted = false
                                and p.modulo = @Modulo
                                and (p.chave = @Chave or p.chave = @AdminChave or (p.recurso = @Recurso and p.acao = @Acao))
                          )
                      )
                );
                """;

            using var connection = _context.CreateConnection();
            return await connection.ExecuteScalarAsync<bool>(Command(sql, new { UsuarioId = usuarioId, Modulo = modulo, Recurso = recurso, Acao = acao, Chave = $"{recurso}.{acao}", AdminChave = $"{modulo}_admin" }, cancellationToken)).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao verificar permissão no schema sigov. UsuarioId={UsuarioId} Modulo={Modulo} Recurso={Recurso} Acao={Acao}", usuarioId, modulo, recurso, acao);
            throw;
        }
    }
}
