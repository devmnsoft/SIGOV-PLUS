using Dapper;
using Microsoft.Extensions.Logging;
using Sigov.Application.Abstractions;
using Sigov.Infrastructure.Persistence.Dapper;

namespace Sigov.Infrastructure.Persistence.Repositories;

public sealed record UsuarioResumoDto(long Id, string Login, string Email, bool Ativo);

public sealed class UsuarioRepository : BaseRepository
{
    private readonly DapperContext _context;
    private readonly ILogger<UsuarioRepository> _logger;
    private readonly ICurrentTenant _currentTenant;

    public UsuarioRepository(DapperContext context, ILogger<UsuarioRepository> logger, ICurrentTenant currentTenant)
    {
        _context = context;
        _logger = logger;
        _currentTenant = currentTenant;
    }

    public async Task<UsuarioResumoDto?> ObterPorLoginAsync(string login, CancellationToken cancellationToken)
    {
        if (!_currentTenant.TenantId.HasValue)
        {
            throw new InvalidOperationException("TenantId obrigatório para obter usuário comum.");
        }

        try
        {
            const string sql = @"select id, login, email, ativo
from sigov.usuario
where tenant_id = @TenantId
  and login = @Login
  and is_deleted = false
limit 1;
";

            using var connection = _context.CreateConnection();
            return await connection.QuerySingleOrDefaultAsync<UsuarioResumoDto>(Command(sql, new { TenantId = _currentTenant.TenantId.Value, Login = login }, cancellationToken)).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao obter usuário no schema sigov. TenantId={TenantId} Login={Login}", _currentTenant.TenantId, login);
            throw;
        }
    }
}
