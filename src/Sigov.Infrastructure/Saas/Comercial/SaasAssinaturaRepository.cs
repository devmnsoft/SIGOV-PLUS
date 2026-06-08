using System.Text.Json;
using Dapper;
using Sigov.Application.Saas.Comercial;
using Sigov.Infrastructure.Persistence.Dapper;

namespace Sigov.Infrastructure.Saas.Comercial;

public sealed class SaasAssinaturaRepository : ISaasAssinaturaRepository
{
    private readonly DapperContext _context;
    public SaasAssinaturaRepository(DapperContext context) => _context = context;

    public async Task<IReadOnlyCollection<SaasAssinaturaResponse>> ListAdminAsync(int offset, int limit, CancellationToken cancellationToken)
    {
        const string sql = """
            select a.id as Id, a.tenant_id as TenantId, a.plano_id as PlanoId, p.codigo as PlanoCodigo, p.nome as PlanoNome, a.status as Status,
                   a.data_inicio as DataInicio, a.data_fim as DataFim, a.usuarios_contratados as UsuariosContratados, a.entidades_contratadas as EntidadesContratadas,
                   a.valor_contratado as ValorContratado, a.periodicidade as Periodicidade,
                   coalesce(array_agg(am.modulo_codigo order by am.modulo_codigo) filter (where am.modulo_codigo is not null), array[]::varchar[]) as Modulos
            from sigov.saas_assinatura a join sigov.saas_plano p on p.id=a.plano_id
            left join sigov.saas_assinatura_modulo am on am.assinatura_id=a.id and am.tenant_id=a.tenant_id and am.habilitado=true
            group by a.id,p.codigo,p.nome
            order by a.created_at desc offset @Offset limit @Limit;
            """;
        using var connection = _context.CreateConnection();
        var rows = await connection.QueryAsync<SaasAssinaturaRow>(new CommandDefinition(sql, new { Offset = offset, Limit = limit }, cancellationToken: cancellationToken)).ConfigureAwait(false);
        return rows.Select(ToResponse).ToArray();
    }

    public Task<SaasAssinaturaResponse?> GetAdminAsync(long id, CancellationToken cancellationToken) => GetAsync("a.id = @Id", new { Id = id }, cancellationToken);
    public Task<SaasAssinaturaResponse?> GetByTenantAsync(long tenantId, CancellationToken cancellationToken) => GetAsync("a.tenant_id = @TenantId", new { TenantId = tenantId }, cancellationToken);

    private async Task<SaasAssinaturaResponse?> GetAsync(string predicate, object parameters, CancellationToken cancellationToken)
    {
        var sql = $"""
            select a.id as Id, a.tenant_id as TenantId, a.plano_id as PlanoId, p.codigo as PlanoCodigo, p.nome as PlanoNome, a.status as Status,
                   a.data_inicio as DataInicio, a.data_fim as DataFim, a.usuarios_contratados as UsuariosContratados, a.entidades_contratadas as EntidadesContratadas,
                   a.valor_contratado as ValorContratado, a.periodicidade as Periodicidade,
                   coalesce(array_agg(am.modulo_codigo order by am.modulo_codigo) filter (where am.modulo_codigo is not null), array[]::varchar[]) as Modulos
            from sigov.saas_assinatura a join sigov.saas_plano p on p.id=a.plano_id
            left join sigov.saas_assinatura_modulo am on am.assinatura_id=a.id and am.tenant_id=a.tenant_id and am.habilitado=true
            where {predicate}
            group by a.id,p.codigo,p.nome
            order by a.created_at desc limit 1;
            """;
        using var connection = _context.CreateConnection();
        var row = await connection.QuerySingleOrDefaultAsync<SaasAssinaturaRow>(new CommandDefinition(sql, parameters, cancellationToken: cancellationToken)).ConfigureAwait(false);
        return row is null ? null : ToResponse(row);
    }

    public async Task<IReadOnlyCollection<string>> GetModulesByTenantAsync(long tenantId, CancellationToken cancellationToken)
    {
        const string sql = "select modulo_codigo from sigov.saas_assinatura_modulo where tenant_id=@TenantId and habilitado=true order by modulo_codigo;";
        using var connection = _context.CreateConnection();
        return (await connection.QueryAsync<string>(new CommandDefinition(sql, new { TenantId = tenantId }, cancellationToken: cancellationToken)).ConfigureAwait(false)).AsList();
    }

    public async Task<IReadOnlyCollection<SaasPlanoLimiteResponse>> GetLimitsByTenantAsync(long tenantId, CancellationToken cancellationToken)
    {
        const string sql = """
            select l.codigo as Codigo, l.nome as Nome, l.valor as Valor, l.unidade as Unidade, l.ilimitado as Ilimitado
            from sigov.saas_assinatura a join sigov.saas_plano_limite l on l.plano_id=a.plano_id
            where a.tenant_id=@TenantId order by l.codigo;
            """;
        using var connection = _context.CreateConnection();
        return (await connection.QueryAsync<SaasPlanoLimiteResponse>(new CommandDefinition(sql, new { TenantId = tenantId }, cancellationToken: cancellationToken)).ConfigureAwait(false)).AsList();
    }

    public async Task<SaasAssinaturaResponse> UpdateAsync(long id, SaasAssinaturaUpdateRequest request, long usuarioId, Guid correlationId, CancellationToken cancellationToken)
    {
        const string sql = """
            update sigov.saas_assinatura set status=@Status, data_fim=@DataFim, usuarios_contratados=@UsuariosContratados, entidades_contratadas=@EntidadesContratadas,
            valor_contratado=@ValorContratado, renovacao_automatica=@RenovacaoAutomatica, updated_at=now(), updated_by=@UsuarioId, correlation_id=@CorrelationId where id=@Id;
            """;
        using var connection = _context.CreateConnection();
        await connection.ExecuteAsync(new CommandDefinition(sql, new { Id = id, request.Status, request.DataFim, request.UsuariosContratados, request.EntidadesContratadas, request.ValorContratado, request.RenovacaoAutomatica, UsuarioId = usuarioId, CorrelationId = correlationId }, cancellationToken: cancellationToken)).ConfigureAwait(false);
        return (await GetAdminAsync(id, cancellationToken).ConfigureAwait(false))!;
    }

    public async Task ChangeStatusAsync(long id, string status, long usuarioId, Guid correlationId, CancellationToken cancellationToken)
    {
        const string sql = "update sigov.saas_assinatura set status=@Status, updated_at=now(), updated_by=@UsuarioId, correlation_id=@CorrelationId where id=@Id;";
        using var connection = _context.CreateConnection();
        await connection.ExecuteAsync(new CommandDefinition(sql, new { Id = id, Status = status, UsuarioId = usuarioId, CorrelationId = correlationId }, cancellationToken: cancellationToken)).ConfigureAwait(false);
    }

    public async Task InsertEventoAsync(long? tenantId, string tipoEvento, string origem, long? origemId, object payload, Guid correlationId, CancellationToken cancellationToken)
    {
        const string sql = "insert into sigov.saas_evento (tenant_id,tipo_evento,origem,origem_id,payload,correlation_id) values (@TenantId,@TipoEvento,@Origem,@OrigemId,cast(@Payload as jsonb),@CorrelationId);";
        using var connection = _context.CreateConnection();
        await connection.ExecuteAsync(new CommandDefinition(sql, new { TenantId = tenantId, TipoEvento = tipoEvento, Origem = origem, OrigemId = origemId, Payload = JsonSerializer.Serialize(payload), CorrelationId = correlationId }, cancellationToken: cancellationToken)).ConfigureAwait(false);
    }

    private static SaasAssinaturaResponse ToResponse(SaasAssinaturaRow row) => new(row.Id, row.TenantId, row.PlanoId, row.PlanoCodigo, row.PlanoNome, row.Status, row.DataInicio, row.DataFim, row.UsuariosContratados, row.EntidadesContratadas, row.ValorContratado, row.Periodicidade, row.Modulos, Array.Empty<SaasPlanoLimiteResponse>());

    private sealed record SaasAssinaturaRow(long Id, long TenantId, long PlanoId, string PlanoCodigo, string PlanoNome, string Status, DateOnly DataInicio, DateOnly? DataFim, int UsuariosContratados, int? EntidadesContratadas, decimal? ValorContratado, string Periodicidade, IReadOnlyCollection<string> Modulos);
}
