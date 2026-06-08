using Dapper;
using Sigov.Application.Agro.Dicionario;
using Sigov.Infrastructure.Agro.Sql;
using Sigov.Infrastructure.Persistence.Dapper;

namespace Sigov.Infrastructure.Agro.Repositories;

public sealed class AgroDicionarioDadosRepository : IAgroDicionarioDadosRepository
{
    private readonly DapperContext _context;
    public AgroDicionarioDadosRepository(DapperContext context) => _context = context;
    public async Task<IReadOnlyCollection<AgroDicionarioDadosResponse>> ListarAsync(long? tenantId, int page, int pageSize, CancellationToken cancellationToken) { using var cn = _context.CreateConnection(); var items = await cn.QueryAsync<AgroDicionarioDadosResponse>(new CommandDefinition(AgroDicionarioDadosSql.Listar, new { TenantId = tenantId, PageSize = pageSize, Offset = (page - 1) * pageSize }, cancellationToken: cancellationToken)).ConfigureAwait(false); return items.ToArray(); }
}
