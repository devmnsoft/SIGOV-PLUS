using System.Text;
using Dapper;

namespace Sigov.Infrastructure.Persistence.Sql;

public sealed class SafeSqlFilterBuilder
{
    private readonly StringBuilder _where = new();
    private readonly DynamicParameters _parameters = new();

    public DynamicParameters Parameters => _parameters;

    public SafeSqlFilterBuilder AddEquals<T>(string columnSql, string parameterName, T? value)
    {
        if (value is null)
        {
            return this;
        }

        AppendAnd();
        _where.Append(columnSql).Append(" = @").Append(parameterName);
        _parameters.Add(parameterName, value);
        return this;
    }

    public SafeSqlFilterBuilder AddIlike(string columnSql, string parameterName, string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return this;
        }

        AppendAnd();
        _where.Append(columnSql).Append(" ilike @").Append(parameterName);
        _parameters.Add(parameterName, $"%{value.Trim()}%");
        return this;
    }

    public string BuildWhereClause() => _where.Length == 0 ? string.Empty : " where " + _where;

    private void AppendAnd()
    {
        if (_where.Length > 0)
        {
            _where.Append(" and ");
        }
    }
}
