using Dapper;
using Sigov.Domain.Agro;
using Sigov.Infrastructure.Persistence.Dapper;

namespace Sigov.Infrastructure.Agro;

public sealed class AgroModuleRepository
{
    private readonly DapperContext _context;

    public AgroModuleRepository(DapperContext context) => _context = context;

    public async Task<bool> TenantPossuiAgroAsync(long tenantId, CancellationToken cancellationToken)
    {
        const string sql = "select exists(select 1 from sigov.tenant_modulo tm join sigov.modulo_saas ms on ms.id = tm.modulo_saas_id where tm.tenant_id=@TenantId and ms.codigo=@Modulo and tm.ativo=true and tm.habilitado=true and tm.contratado=true and tm.is_deleted=false);";
        using var connection = _context.CreateConnection();
        return await connection.ExecuteScalarAsync<bool>(new CommandDefinition(sql, new { TenantId = tenantId, Modulo = AgroModulo.Codigo }, cancellationToken: cancellationToken)).ConfigureAwait(false);
    }
}
