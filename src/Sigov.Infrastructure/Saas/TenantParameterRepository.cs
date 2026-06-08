using Dapper;
using Sigov.Application.Saas.Parameters;
using Sigov.Infrastructure.Persistence.Dapper;

namespace Sigov.Infrastructure.Saas;

public sealed class TenantParameterRepository : ITenantParameterRepository
{
    private readonly DapperContext _context;

    public TenantParameterRepository(DapperContext context) => _context = context;

    public async Task<IReadOnlyCollection<TenantParameterDefinitionDto>> GetDefinitionsAsync(CancellationToken cancellationToken)
    {
        const string sql = """
            select id as Id, codigo as Codigo, nome as Nome, descricao as Descricao, modulo as Modulo, tipo_parametro as TipoParametro,
                   escopo as Escopo, valor_padrao::text as ValorPadraoJson, obrigatorio as Obrigatorio, sensivel as Sensivel,
                   editavel_tenant as EditavelTenant, ativo as Ativo
            from sigov.tenant_parametro_definicao
            where ativo = true
            order by modulo nulls first, codigo;
            """;
        using var connection = _context.CreateConnection();
        var rows = await connection.QueryAsync<TenantParameterDefinitionDto>(new CommandDefinition(sql, cancellationToken: cancellationToken)).ConfigureAwait(false);
        return rows.AsList();
    }

    public async Task<TenantParameterDefinitionDto?> GetDefinitionAsync(string codigo, CancellationToken cancellationToken)
    {
        const string sql = """
            select id as Id, codigo as Codigo, nome as Nome, descricao as Descricao, modulo as Modulo, tipo_parametro as TipoParametro,
                   escopo as Escopo, valor_padrao::text as ValorPadraoJson, obrigatorio as Obrigatorio, sensivel as Sensivel,
                   editavel_tenant as EditavelTenant, ativo as Ativo
            from sigov.tenant_parametro_definicao
            where codigo = @Codigo and ativo = true
            limit 1;
            """;
        using var connection = _context.CreateConnection();
        return await connection.QuerySingleOrDefaultAsync<TenantParameterDefinitionDto>(new CommandDefinition(sql, new { Codigo = codigo }, cancellationToken: cancellationToken)).ConfigureAwait(false);
    }

    public async Task<IReadOnlyCollection<TenantParameterValueDto>> GetValuesAsync(string codigo, TenantParameterResolveContext context, CancellationToken cancellationToken)
    {
        const string sql = """
            select v.id as Id, v.tenant_id as TenantId, v.entidade_id as EntidadeId, v.exercicio_id as ExercicioId, v.usuario_id as UsuarioId,
                   v.modulo_codigo as ModuloCodigo, v.escopo as Escopo, v.valor::text as ValorJson, v.valor_mascarado as ValorMascarado,
                   v.vigente_inicio as VigenteInicio, v.vigente_fim as VigenteFim, v.ativo as Ativo
            from sigov.tenant_parametro_valor v
            join sigov.tenant_parametro_definicao d on d.id = v.parametro_definicao_id
            where d.codigo = @Codigo
              and v.tenant_id = @TenantId
              and v.ativo = true
              and (v.entidade_id is null or v.entidade_id = @EntidadeId)
              and (v.exercicio_id is null or v.exercicio_id = @ExercicioId)
              and (v.usuario_id is null or v.usuario_id = @UsuarioId)
              and (v.modulo_codigo is null or v.modulo_codigo = @ModuloCodigo)
            order by v.created_at desc;
            """;
        using var connection = _context.CreateConnection();
        var rows = await connection.QueryAsync<TenantParameterValueDto>(new CommandDefinition(sql, new { Codigo = codigo, context.TenantId, context.EntidadeId, context.ExercicioId, context.UsuarioId, context.ModuloCodigo }, cancellationToken: cancellationToken)).ConfigureAwait(false);
        return rows.AsList();
    }

    public async Task UpsertValueAsync(string codigo, TenantParameterValueDto value, long? userId, Guid? correlationId, CancellationToken cancellationToken)
    {
        const string sql = """
            with definicao as (
                select id from sigov.tenant_parametro_definicao where codigo = @Codigo
            ), atualizado as (
                update sigov.tenant_parametro_valor v
                set valor = @Valor::jsonb,
                    valor_mascarado = @ValorMascarado,
                    vigente_inicio = @VigenteInicio,
                    vigente_fim = @VigenteFim,
                    ativo = @Ativo,
                    updated_at = now(),
                    updated_by = @UserId,
                    correlation_id = @CorrelationId
                from definicao d
                where v.parametro_definicao_id = d.id
                  and v.tenant_id = @TenantId
                  and v.escopo = @Escopo
                  and coalesce(v.entidade_id, 0) = coalesce(@EntidadeId, 0)
                  and coalesce(v.exercicio_id, 0) = coalesce(@ExercicioId, 0)
                  and coalesce(v.usuario_id, 0) = coalesce(@UsuarioId, 0)
                  and coalesce(v.modulo_codigo, '') = coalesce(@ModuloCodigo, '')
                returning v.id
            )
            insert into sigov.tenant_parametro_valor
                (tenant_id, entidade_id, exercicio_id, usuario_id, modulo_codigo, escopo, parametro_definicao_id, valor, valor_mascarado, vigente_inicio, vigente_fim, ativo, created_by, correlation_id)
            select @TenantId, @EntidadeId, @ExercicioId, @UsuarioId, @ModuloCodigo, @Escopo, d.id, @Valor::jsonb, @ValorMascarado, @VigenteInicio, @VigenteFim, @Ativo, @UserId, @CorrelationId
            from definicao d
            where not exists (select 1 from atualizado);
            """;
        using var connection = _context.CreateConnection();
        await connection.ExecuteAsync(new CommandDefinition(sql, new { Codigo = codigo, value.TenantId, value.EntidadeId, value.ExercicioId, value.UsuarioId, value.ModuloCodigo, value.Escopo, Valor = value.ValorJson, value.ValorMascarado, value.VigenteInicio, value.VigenteFim, value.Ativo, UserId = userId, CorrelationId = correlationId }, cancellationToken: cancellationToken)).ConfigureAwait(false);
    }
}
