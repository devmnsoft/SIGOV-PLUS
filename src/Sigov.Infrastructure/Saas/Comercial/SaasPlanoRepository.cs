using System.Text.Json;
using Dapper;
using Sigov.Application.Saas.Comercial;
using Sigov.Infrastructure.Persistence.Dapper;

namespace Sigov.Infrastructure.Saas.Comercial;

public sealed class SaasPlanoRepository : ISaasPlanoRepository
{
    private readonly DapperContext _context;
    public SaasPlanoRepository(DapperContext context) => _context = context;

    public async Task<IReadOnlyCollection<SaasPlanoResponse>> ListPublicAsync(CancellationToken cancellationToken) => await ListAsync("where p.publico = true and p.ativo = true", 0, 100, cancellationToken).ConfigureAwait(false);
    public async Task<IReadOnlyCollection<SaasPlanoResponse>> ListAdminAsync(int offset, int limit, CancellationToken cancellationToken) => await ListAsync(string.Empty, offset, limit, cancellationToken).ConfigureAwait(false);

    private async Task<IReadOnlyCollection<SaasPlanoResponse>> ListAsync(string whereClause, int offset, int limit, CancellationToken cancellationToken)
    {
        var sql = $@"select p.id as Id, p.codigo as Codigo, p.nome as Nome, p.descricao as Descricao, p.publico as Publico, p.destaque as Destaque, p.ordem as Ordem,
       p.tipo_plano as TipoPlano, p.preco_base as PrecoBase, p.moeda as Moeda, p.periodicidade as Periodicidade, p.limite_usuarios as LimiteUsuarios,
       p.permite_white_label as PermiteWhiteLabel, p.permite_dominio_customizado as PermiteDominioCustomizado, p.ativo as Ativo,
       coalesce(array_agg(pm.modulo_codigo order by pm.modulo_codigo) filter (where pm.modulo_codigo is not null), array[]::varchar[]) as Modulos
from sigov.saas_plano p
left join sigov.saas_plano_modulo pm on pm.plano_id = p.id and pm.incluso = true
{whereClause}
group by p.id
order by p.ordem, p.nome
offset @Offset limit @Limit;
";
        using var connection = _context.CreateConnection();
        var rows = await connection.QueryAsync<SaasPlanoResponse>(new CommandDefinition(sql, new { Offset = offset, Limit = limit }, cancellationToken: cancellationToken)).ConfigureAwait(false);
        return rows.AsList();
    }

    public async Task<SaasPlanoDetalheResponse?> GetByCodigoAsync(string codigo, CancellationToken cancellationToken)
    {
        const string planoSql = @"select p.id as Id, p.codigo as Codigo, p.nome as Nome, p.descricao as Descricao, p.publico as Publico, p.destaque as Destaque, p.ordem as Ordem,
       p.tipo_plano as TipoPlano, p.preco_base as PrecoBase, p.moeda as Moeda, p.periodicidade as Periodicidade, p.limite_usuarios as LimiteUsuarios,
       p.permite_white_label as PermiteWhiteLabel, p.permite_dominio_customizado as PermiteDominioCustomizado, p.ativo as Ativo,
       coalesce(array_agg(pm.modulo_codigo order by pm.modulo_codigo) filter (where pm.modulo_codigo is not null), array[]::varchar[]) as Modulos
from sigov.saas_plano p left join sigov.saas_plano_modulo pm on pm.plano_id=p.id and pm.incluso=true
where p.codigo = @Codigo and p.ativo = true
group by p.id;
";
        using var connection = _context.CreateConnection();
        var plano = await connection.QuerySingleOrDefaultAsync<SaasPlanoResponse>(new CommandDefinition(planoSql, new { Codigo = codigo.Trim().ToUpperInvariant() }, cancellationToken: cancellationToken)).ConfigureAwait(false);
        if (plano is null) return null;
        var limites = (await connection.QueryAsync<SaasPlanoLimiteResponse>(new CommandDefinition("select codigo as Codigo, nome as Nome, valor as Valor, unidade as Unidade, ilimitado as Ilimitado from sigov.saas_plano_limite where plano_id=@PlanoId order by codigo;", new { PlanoId = plano.Id }, cancellationToken: cancellationToken)).ConfigureAwait(false)).AsList();
        var addons = (await connection.QueryAsync<SaasAddonResponse>(new CommandDefinition("select a.id as Id, a.codigo as Codigo, a.nome as Nome, a.tipo_addon as TipoAddon, a.modulo_codigo as ModuloCodigo, a.preco as Preco, a.periodicidade as Periodicidade from sigov.saas_addon a where a.ativo = true order by a.nome;", cancellationToken: cancellationToken)).ConfigureAwait(false)).AsList();
        return new SaasPlanoDetalheResponse(plano, limites, addons);
    }

    public async Task<SaasPlanoResponse> CreateAsync(SaasPlanoCreateRequest request, long usuarioId, Guid correlationId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Codigo))
        {
            throw new ArgumentException("Código do plano é obrigatório.", nameof(request.Codigo));
        }

        var codigo = request.Codigo.Trim().ToUpperInvariant();

        const string sql = @"insert into sigov.saas_plano (codigo,nome,descricao,publico,destaque,ordem,tipo_plano,preco_base,periodicidade,limite_usuarios,limite_entidades,limite_armazenamento_mb,permite_white_label,permite_dominio_customizado)
values (@Codigo,@Nome,@Descricao,@Publico,@Destaque,@Ordem,@TipoPlano,@PrecoBase,@Periodicidade,@LimiteUsuarios,@LimiteEntidades,@LimiteArmazenamentoMb,@PermiteWhiteLabel,@PermiteDominioCustomizado)
returning id;
";
        using var connection = _context.CreateConnection();
        var id = await connection.ExecuteScalarAsync<long>(new CommandDefinition(sql, request with { Codigo = codigo }, cancellationToken: cancellationToken)).ConfigureAwait(false);
        foreach (var modulo in request.Modulos.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            await connection.ExecuteAsync(new CommandDefinition("insert into sigov.saas_plano_modulo (plano_id, modulo_codigo) values (@PlanoId,@Modulo) on conflict (plano_id, modulo_codigo) do nothing;", new { PlanoId = id, Modulo = modulo.Trim().ToLowerInvariant() }, cancellationToken: cancellationToken)).ConfigureAwait(false);
        }
        return (await GetByCodigoAsync(codigo, cancellationToken).ConfigureAwait(false))!.Plano;
    }

    public async Task<SaasPlanoResponse> UpdateAsync(long id, SaasPlanoUpdateRequest request, long usuarioId, Guid correlationId, CancellationToken cancellationToken)
    {
        const string sql = @"update sigov.saas_plano set nome=@Nome, descricao=@Descricao, publico=@Publico, destaque=@Destaque, ordem=@Ordem, preco_base=@PrecoBase,
    limite_usuarios=@LimiteUsuarios, permite_white_label=@PermiteWhiteLabel, permite_dominio_customizado=@PermiteDominioCustomizado, ativo=@Ativo
where id=@Id returning codigo;
";
        using var connection = _context.CreateConnection();
        var codigo = await connection.ExecuteScalarAsync<string>(new CommandDefinition(sql, new { Id = id, request.Nome, request.Descricao, request.Publico, request.Destaque, request.Ordem, request.PrecoBase, request.LimiteUsuarios, request.PermiteWhiteLabel, request.PermiteDominioCustomizado, request.Ativo }, cancellationToken: cancellationToken)).ConfigureAwait(false);
        return (await GetByCodigoAsync(codigo, cancellationToken).ConfigureAwait(false))!.Plano;
    }

    public async Task InsertEventoAsync(long? tenantId, string tipoEvento, string origem, long? origemId, object payload, Guid correlationId, CancellationToken cancellationToken)
    {
        const string sql = "insert into sigov.saas_evento (tenant_id,tipo_evento,origem,origem_id,payload,correlation_id) values (@TenantId,@TipoEvento,@Origem,@OrigemId,cast(@Payload as jsonb),@CorrelationId);";
        using var connection = _context.CreateConnection();
        await connection.ExecuteAsync(new CommandDefinition(sql, new { TenantId = tenantId, TipoEvento = tipoEvento, Origem = origem, OrigemId = origemId, Payload = JsonSerializer.Serialize(payload), CorrelationId = correlationId }, cancellationToken: cancellationToken)).ConfigureAwait(false);
    }
}
