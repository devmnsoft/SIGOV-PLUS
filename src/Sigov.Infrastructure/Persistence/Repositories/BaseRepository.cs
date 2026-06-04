using System.Data;
using Dapper;
using Sigov.Application.Common;

namespace Sigov.Infrastructure.Persistence.Repositories;

public abstract class BaseRepository
{
    protected static CommandDefinition Command(string sql, object? parameters = null, CancellationToken cancellationToken = default)
    {
        return new CommandDefinition(sql, parameters, cancellationToken: cancellationToken);
    }

    protected static object Pagination(PaginationQuery query) => new { Limit = query.SafePageSize, Offset = query.Offset };
}
