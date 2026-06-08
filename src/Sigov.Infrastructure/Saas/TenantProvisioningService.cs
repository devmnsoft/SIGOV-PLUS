using Dapper;
using Sigov.Application.Saas;
using Sigov.Infrastructure.Persistence.Dapper;

namespace Sigov.Infrastructure.Saas;

public sealed class TenantProvisioningService : ITenantProvisioningService
{
    private readonly NpgsqlConnectionFactory _connectionFactory;

    public TenantProvisioningService(NpgsqlConnectionFactory connectionFactory) => _connectionFactory = connectionFactory;

    public async Task<ProvisionTenantResult> ProvisionarAsync(ProvisionTenantRequest request, CancellationToken cancellationToken)
    {
        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var tx = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var tenantId = await connection.ExecuteScalarAsync<long>(new CommandDefinition(@"insert into sigov.tenant (nome, documento, slug, status, ambiente, data_inicio_operacao)
values (@Nome, @Documento, @Slug, 'IMPLANTACAO', @Ambiente, now())
on conflict (slug) do update set nome = excluded.nome, updated_at = now()
returning id;
", new { Nome = request.NomeTenant, request.Documento, request.Slug, request.Ambiente }, tx, cancellationToken: cancellationToken)).ConfigureAwait(false);

            if (!string.IsNullOrWhiteSpace(request.Dominio))
            {
                await connection.ExecuteAsync(new CommandDefinition(@"insert into sigov.tenant_dominio (tenant_id, tipo, dominio, principal, verificado)
values (@TenantId, 'SUBDOMINIO_SIGOV', @Dominio, true, true)
on conflict (dominio) do update set tenant_id = excluded.tenant_id, updated_at = now();
", new { TenantId = tenantId, request.Dominio }, tx, cancellationToken: cancellationToken)).ConfigureAwait(false);
            }

            var planoId = await connection.ExecuteScalarAsync<long>(new CommandDefinition(@"insert into sigov.plano_saas (codigo, nome, ativo)
values (@PlanoCodigo, @PlanoCodigo, true)
on conflict (codigo) do update set ativo = true, updated_at = now()
returning id;
", new { request.PlanoCodigo }, tx, cancellationToken: cancellationToken)).ConfigureAwait(false);

            await connection.ExecuteAsync(new CommandDefinition(@"insert into sigov.tenant_assinatura (tenant_id, plano_saas_id, status, inicio_at, ativo)
values (@TenantId, @PlanoId, 'ATIVA', now(), true)
on conflict do nothing;
", new { TenantId = tenantId, PlanoId = planoId }, tx, cancellationToken: cancellationToken)).ConfigureAwait(false);

            foreach (var modulo in request.Modulos ?? Array.Empty<string>())
            {
                var moduloId = await connection.ExecuteScalarAsync<long>(new CommandDefinition(@"insert into sigov.modulo_saas (codigo, nome, categoria, ativo)
values (@Codigo, @Codigo, upper(@Codigo), true)
on conflict (codigo) do update set ativo = true
returning id;
", new { Codigo = modulo }, tx, cancellationToken: cancellationToken)).ConfigureAwait(false);
                await connection.ExecuteAsync(new CommandDefinition(@"insert into sigov.tenant_modulo (tenant_id, modulo_saas_id, habilitado, contratado, ativo)
values (@TenantId, @ModuloId, true, true, true)
on conflict (tenant_id, modulo_saas_id) do update set habilitado = true, contratado = true, ativo = true, updated_at = now();
", new { TenantId = tenantId, ModuloId = moduloId }, tx, cancellationToken: cancellationToken)).ConfigureAwait(false);
            }

            var entidadeId = await connection.ExecuteScalarAsync<long?>(new CommandDefinition(@"insert into sigov.entidade (tenant_id, nome, cnpj, ativo)
values (@TenantId, @NomeEntidade, coalesce(@CnpjEntidade, '00000000000000'), true)
on conflict do nothing
returning id;
", new { TenantId = tenantId, request.NomeEntidade, request.CnpjEntidade }, tx, cancellationToken: cancellationToken)).ConfigureAwait(false);

            long? exercicioId = null;
            long? usuarioId = null;
            if (entidadeId.HasValue)
            {
                exercicioId = await connection.ExecuteScalarAsync<long>(new CommandDefinition(@"insert into sigov.exercicio (tenant_id, entidade_id, ano, data_inicio, data_fim, ativo)
values (@TenantId, @EntidadeId, @Ano, make_date(@Ano, 1, 1), make_date(@Ano, 12, 31), true)
returning id;
", new { TenantId = tenantId, EntidadeId = entidadeId.Value, Ano = request.AnoExercicio }, tx, cancellationToken: cancellationToken)).ConfigureAwait(false);

                var pessoaId = await connection.ExecuteScalarAsync<long>(new CommandDefinition(@"insert into sigov.pessoa (tenant_id, entidade_id, exercicio_id, tipo_pessoa, nome, documento, ativo)
values (@TenantId, @EntidadeId, @ExercicioId, 'F', @AdminNome, @AdminEmail, true)
returning id;
", new { TenantId = tenantId, EntidadeId = entidadeId.Value, ExercicioId = exercicioId.Value, request.AdminNome, request.AdminEmail }, tx, cancellationToken: cancellationToken)).ConfigureAwait(false);

                usuarioId = await connection.ExecuteScalarAsync<long>(new CommandDefinition(@"insert into sigov.usuario (tenant_id, entidade_id, exercicio_id, pessoa_id, login, email, senha_hash, tipo_usuario, ativo)
values (@TenantId, @EntidadeId, @ExercicioId, @PessoaId, @AdminLogin, @AdminEmail, 'INVITE_REQUIRED', 'TENANT_ADMIN', true)
returning id;
", new { TenantId = tenantId, EntidadeId = entidadeId.Value, ExercicioId = exercicioId.Value, PessoaId = pessoaId, request.AdminLogin, request.AdminEmail }, tx, cancellationToken: cancellationToken)).ConfigureAwait(false);
            }

            await connection.ExecuteAsync(new CommandDefinition(@"insert into sigov.tenant_evento_operacional (tenant_id, tipo, severidade, mensagem, payload)
values (@TenantId, 'TENANT_PROVISIONED', 'INFO', 'Tenant provisionado com fluxo transacional SaaS.', '{}'::jsonb);
", new { TenantId = tenantId }, tx, cancellationToken: cancellationToken)).ConfigureAwait(false);

            await tx.CommitAsync(cancellationToken).ConfigureAwait(false);
            return new ProvisionTenantResult(tenantId, request.Slug, entidadeId, exercicioId, usuarioId, false);
        }
        catch
        {
            await tx.RollbackAsync(cancellationToken).ConfigureAwait(false);
            throw;
        }
    }
}
