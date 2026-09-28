using System.Data;
using Dapper;

namespace Sigov.Infrastructure.Persistence.Dapper;

/// <summary>
/// Habilita parametros System.DateOnly e DateOnly? no Dapper para colunas date do PostgreSQL,
/// cujos valores sao aceitos nativamente pelo Npgsql. Sem este handler o Dapper lanca
/// NotSupportedException ao gerar os metadados de parametros de comandos que usam dates.
/// </summary>
public sealed class DateOnlyDapperTypeHandler : SqlMapper.ITypeHandler
{
    public void SetValue(IDbDataParameter parameter, object value)
    {
        ArgumentNullException.ThrowIfNull(parameter);
        parameter.Value = value is null ? DBNull.Value : value;
    }

    public DbType? GetDbType(Type type)
        => type == typeof(DateOnly) || type == typeof(DateOnly?) ? DbType.Date : null;

    public object Parse(Type type, object value)
        => value;
}
