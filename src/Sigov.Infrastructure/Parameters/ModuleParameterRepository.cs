using System.Text.Json;
using Dapper;
using Sigov.Application.Parameters;
using Sigov.Infrastructure.Persistence.Dapper;

namespace Sigov.Infrastructure.Parameters;

public sealed class ModuleParameterRepository : IModuleParameterRepository
{
    private readonly DapperContext _context;
    public ModuleParameterRepository(DapperContext context) => _context = context;

    public async Task<IReadOnlyCollection<ModuleParameterValue>> ListAsync(long tenantId, string module, CancellationToken cancellationToken)
    {
        const string sql = @"
select p.codigo as Code, p.nome as Name, p.tipo as Type,
       coalesce(v.valor::text, p.valor_padrao::text) as ValueJson,
       p.sensivel as Sensitive, coalesce(v.updated_at, v.created_at, p.created_at) as UpdatedAt
from sigov.parametro_modulo p
left join sigov.parametro_modulo_valor v on v.parametro_id=p.id and v.tenant_id=@TenantId and v.is_deleted=false
where p.modulo=@Module and p.ativo=true and p.is_deleted=false
order by p.ordem, p.codigo;
";
        using var connection = _context.CreateConnection();
        var rows = await connection.QueryAsync<ModuleParameterValue>(new CommandDefinition(sql, new { TenantId = tenantId, Module = module }, cancellationToken: cancellationToken)).ConfigureAwait(false);
        return rows.AsList();
    }

    public async Task<IReadOnlyCollection<ModuleParameterHistory>> HistoryAsync(long tenantId, string module, int page, int pageSize, CancellationToken cancellationToken)
    {
        const string sql = @"
select h.id as Id, p.codigo as Code, h.valor_anterior::text as PreviousValueJson, h.valor_novo::text as ValueJson,
       h.usuario_id as ChangedBy, h.correlation_id as CorrelationId, h.created_at as ChangedAt
from sigov.parametro_modulo_historico h
join sigov.parametro_modulo p on p.id=h.parametro_id
where h.tenant_id=@TenantId and p.modulo=@Module
order by h.created_at desc, h.id desc offset @Offset limit @PageSize;
";
        using var connection = _context.CreateConnection();
        var rows = await connection.QueryAsync<ModuleParameterHistory>(new CommandDefinition(sql, new { TenantId = tenantId, Module = module, Offset = (page - 1) * pageSize, PageSize = pageSize }, cancellationToken: cancellationToken)).ConfigureAwait(false);
        return rows.AsList();
    }

    public async Task SaveAsync(long tenantId, string module, IReadOnlyDictionary<string, string> values, long? userId, string correlationId, CancellationToken cancellationToken)
    {
        const string sql = "select sigov.salvar_parametro_modulo(@TenantId,@Module,@Code,cast(@Value as jsonb),@UserId,@CorrelationId);";
        using var connection = _context.CreateConnection();
        connection.Open();
        using var transaction = connection.BeginTransaction();
        foreach (var item in values)
        {
            using var document = JsonDocument.Parse(item.Value);
            await connection.ExecuteAsync(new CommandDefinition(sql, new { TenantId = tenantId, Module = module, Code = item.Key.Trim().ToUpperInvariant(), Value = document.RootElement.GetRawText(), UserId = userId, CorrelationId = correlationId }, transaction, cancellationToken: cancellationToken)).ConfigureAwait(false);
        }
        transaction.Commit();
    }
}
