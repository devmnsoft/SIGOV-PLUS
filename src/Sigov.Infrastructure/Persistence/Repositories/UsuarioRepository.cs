using Dapper;
using Microsoft.Extensions.Logging;
using Sigov.Infrastructure.Persistence.Dapper;

namespace Sigov.Infrastructure.Persistence.Repositories;

public sealed record UsuarioResumoDto(long Id, string Login, string Email, bool Ativo);

public sealed class UsuarioRepository : BaseRepository
{
    private readonly DapperContext _context;
    private readonly ILogger<UsuarioRepository> _logger;

    public UsuarioRepository(DapperContext context, ILogger<UsuarioRepository> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<UsuarioResumoDto?> ObterPorLoginAsync(string login, CancellationToken cancellationToken)
    {
        try
        {
            const string sql = """
                select id, login, email, ativo
                from sigov.usuario
                where login = @Login and is_deleted = false
                limit 1;
                """;

            using var connection = _context.CreateConnection();
            return await connection.QuerySingleOrDefaultAsync<UsuarioResumoDto>(Command(sql, new { Login = login }, cancellationToken)).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao obter usuário no schema sigov. Login={Login}", login);
            throw;
        }
    }
}
