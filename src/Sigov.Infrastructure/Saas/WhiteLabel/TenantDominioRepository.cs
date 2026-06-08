using System.Text.Json;
using Dapper;
using Sigov.Application.Saas.WhiteLabel;
using Sigov.Infrastructure.Persistence.Dapper;

namespace Sigov.Infrastructure.Saas.WhiteLabel;

public sealed class TenantDominioRepository : ITenantDominioRepository
{
    private readonly DapperContext _context;
    public TenantDominioRepository(DapperContext context) => _context = context;

    public async Task<IReadOnlyCollection<TenantDominioResponse>> ListAsync(long tenantId, CancellationToken cancellationToken)
    {
        const string sql = "select id as Id, tenant_id as TenantId, dominio as Dominio, status as Status, verificado as Verificado, token_verificacao as TokenVerificacao, ssl_status as SslStatus from sigov.saas_tenant_dominio where tenant_id=@TenantId order by created_at desc;";
        using var connection = _context.CreateConnection();
        return (await connection.QueryAsync<TenantDominioResponse>(new CommandDefinition(sql, new { TenantId = tenantId }, cancellationToken: cancellationToken)).ConfigureAwait(false)).AsList();
    }

    public async Task<bool> PlanoPermiteDominioAsync(long tenantId, CancellationToken cancellationToken)
    {
        const string sql = "select exists(select 1 from sigov.saas_assinatura a join sigov.saas_plano p on p.id=a.plano_id where a.tenant_id=@TenantId and (p.permite_dominio_customizado=true or exists(select 1 from sigov.saas_assinatura_addon aa where aa.tenant_id=a.tenant_id and aa.assinatura_id=a.id and aa.addon_codigo='DOMINIO_CUSTOMIZADO' and aa.status='ATIVO')));";
        using var connection = _context.CreateConnection();
        return await connection.ExecuteScalarAsync<bool>(new CommandDefinition(sql, new { TenantId = tenantId }, cancellationToken: cancellationToken)).ConfigureAwait(false);
    }

    public async Task<TenantDominioResponse> CreateAsync(long tenantId, string dominio, string tokenHash, long usuarioId, Guid correlationId, CancellationToken cancellationToken)
    {
        const string sql = "insert into sigov.saas_tenant_dominio (tenant_id,dominio,status,token_verificacao,ssl_status) values (@TenantId,@Dominio,'PENDENTE_VERIFICACAO',@Token,'PENDENTE') returning id;";
        using var connection = _context.CreateConnection();
        var id = await connection.ExecuteScalarAsync<long>(new CommandDefinition(sql, new { TenantId = tenantId, Dominio = dominio.Trim().ToLowerInvariant(), Token = tokenHash }, cancellationToken: cancellationToken)).ConfigureAwait(false);
        return (await ListAsync(tenantId, cancellationToken).ConfigureAwait(false)).Single(x => x.Id == id);
    }

    public async Task<TenantDominioResponse> VerifyAsync(long tenantId, long id, long usuarioId, Guid correlationId, CancellationToken cancellationToken)
    {
        const string sql = "update sigov.saas_tenant_dominio set status='VERIFICADO', verificado=true, ssl_status='PENDENTE', updated_at=now() where tenant_id=@TenantId and id=@Id;";
        using var connection = _context.CreateConnection();
        await connection.ExecuteAsync(new CommandDefinition(sql, new { TenantId = tenantId, Id = id }, cancellationToken: cancellationToken)).ConfigureAwait(false);
        return (await ListAsync(tenantId, cancellationToken).ConfigureAwait(false)).Single(x => x.Id == id);
    }

    public async Task InsertEventoAsync(long? tenantId, string tipoEvento, string origem, long? origemId, object payload, Guid correlationId, CancellationToken cancellationToken)
    {
        const string sql = "insert into sigov.saas_evento (tenant_id,tipo_evento,origem,origem_id,payload,correlation_id) values (@TenantId,@TipoEvento,@Origem,@OrigemId,cast(@Payload as jsonb),@CorrelationId);";
        using var connection = _context.CreateConnection();
        await connection.ExecuteAsync(new CommandDefinition(sql, new { TenantId = tenantId, TipoEvento = tipoEvento, Origem = origem, OrigemId = origemId, Payload = JsonSerializer.Serialize(payload), CorrelationId = correlationId }, cancellationToken: cancellationToken)).ConfigureAwait(false);
    }
}
