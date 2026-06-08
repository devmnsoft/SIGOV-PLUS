using System.Text.Json;
using Dapper;
using Sigov.Application.Saas.Perfis;
using Sigov.Infrastructure.Persistence.Dapper;

namespace Sigov.Infrastructure.Saas.Perfis;

public sealed class SaasPerfilTemplateRepository : ISaasPerfilTemplateRepository
{
    private readonly DapperContext _context;
    public SaasPerfilTemplateRepository(DapperContext context) => _context = context;

    public async Task<IReadOnlyCollection<SaasPerfilTemplateResponse>> ListAsync(int offset, int limit, CancellationToken cancellationToken)
    {
        const string sql = "select id as Id,codigo as Codigo,nome as Nome,nivel_base as NivelBase,descricao as Descricao,array[]::varchar[] as Permissoes,ativo as Ativo from sigov.saas_perfil_template order by nome offset @Offset limit @Limit;";
        using var connection = _context.CreateConnection();
        return (await connection.QueryAsync<SaasPerfilTemplateResponse>(new CommandDefinition(sql, new { Offset = offset, Limit = limit }, cancellationToken: cancellationToken)).ConfigureAwait(false)).AsList();
    }

    public async Task<SaasPerfilTemplateResponse> CreateAsync(SaasPerfilTemplateResponse request, long usuarioId, Guid correlationId, CancellationToken cancellationToken)
    {
        const string sql = """
            insert into sigov.saas_perfil_template (codigo,nome,nivel_base,descricao,permissoes_json,ativo)
            values (@Codigo,@Nome,@NivelBase,@Descricao,cast(@Permissoes as jsonb),@Ativo)
            on conflict (codigo) do update set nome=excluded.nome,nivel_base=excluded.nivel_base,descricao=excluded.descricao,permissoes_json=excluded.permissoes_json,ativo=excluded.ativo
            returning id as Id,codigo as Codigo,nome as Nome,nivel_base as NivelBase,descricao as Descricao,array[]::varchar[] as Permissoes,ativo as Ativo;
            """;
        using var connection = _context.CreateConnection();
        return await connection.QuerySingleAsync<SaasPerfilTemplateResponse>(new CommandDefinition(sql, new { Codigo = request.Codigo.ToUpperInvariant(), request.Nome, NivelBase = request.NivelBase.ToUpperInvariant(), request.Descricao, Permissoes = JsonSerializer.Serialize(request.Permissoes), request.Ativo }, cancellationToken: cancellationToken)).ConfigureAwait(false);
    }

    public async Task CriarPerfisTenantPorTemplateAsync(CriarPerfisTenantPorTemplateRequest request, long usuarioId, Guid correlationId, CancellationToken cancellationToken)
    {
        const string sql = """
            insert into sigov.perfil_acesso (tenant_id,nome,descricao,codigo_externo,created_by,correlation_id)
            select @TenantId, nome, descricao, codigo, @UsuarioId, @CorrelationId from sigov.saas_perfil_template
            where ativo=true and codigo = any(@Templates) and nivel_base <> 'ADMINISTRADOR_GERAL'
            on conflict do nothing;
            """;
        using var connection = _context.CreateConnection();
        await connection.ExecuteAsync(new CommandDefinition(sql, new { request.TenantId, Templates = request.TemplatesCodigos.Select(x => x.ToUpperInvariant()).ToArray(), UsuarioId = usuarioId, CorrelationId = correlationId }, cancellationToken: cancellationToken)).ConfigureAwait(false);
    }

    public async Task InsertEventoAsync(long? tenantId, string tipoEvento, string origem, long? origemId, object payload, Guid correlationId, CancellationToken cancellationToken)
    {
        const string sql = "insert into sigov.saas_evento (tenant_id,tipo_evento,origem,origem_id,payload,correlation_id) values (@TenantId,@TipoEvento,@Origem,@OrigemId,cast(@Payload as jsonb),@CorrelationId);";
        using var connection = _context.CreateConnection();
        await connection.ExecuteAsync(new CommandDefinition(sql, new { TenantId = tenantId, TipoEvento = tipoEvento, Origem = origem, OrigemId = origemId, Payload = JsonSerializer.Serialize(payload), CorrelationId = correlationId }, cancellationToken: cancellationToken)).ConfigureAwait(false);
    }
}
