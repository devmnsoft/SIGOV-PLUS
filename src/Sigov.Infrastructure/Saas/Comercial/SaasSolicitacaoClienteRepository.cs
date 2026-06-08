using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Dapper;
using Sigov.Application.Saas.Comercial;
using Sigov.Infrastructure.Persistence.Dapper;

namespace Sigov.Infrastructure.Saas.Comercial;

public sealed class SaasSolicitacaoClienteRepository : ISaasSolicitacaoClienteRepository
{
    private readonly DapperContext _context;
    public SaasSolicitacaoClienteRepository(DapperContext context) => _context = context;

    public async Task<SaasSolicitacaoClienteResponse> CreateAsync(SaasSolicitacaoClienteCreateRequest request, string protocolo, Guid correlationId, CancellationToken cancellationToken)
    {
        const string sql = """
            insert into sigov.saas_solicitacao_cliente (protocolo,nome_organizacao,tipo_cliente,documento,cidade,uf,nome_responsavel,email_responsavel,telefone_responsavel,plano_codigo,modulos_interesse,usuarios_estimados,entidades_estimadas,deseja_white_label,deseja_dominio_customizado,dominio_desejado,status,correlation_id)
            values (@Protocolo,@NomeOrganizacao,@TipoCliente,@Documento,@Cidade,@Uf,@NomeResponsavel,@EmailResponsavel,@TelefoneResponsavel,@PlanoCodigo,cast(@ModulosInteresse as jsonb),@UsuariosEstimados,@EntidadesEstimadas,@DesejaWhiteLabel,@DesejaDominioCustomizado,@DominioDesejado,'RECEBIDA',@CorrelationId)
            returning id;
            """;
        using var connection = _context.CreateConnection();
        var id = await connection.ExecuteScalarAsync<long>(new CommandDefinition(sql, new { Protocolo = protocolo, request.NomeOrganizacao, request.TipoCliente, request.Documento, request.Cidade, Uf = request.Uf?.ToUpperInvariant(), request.NomeResponsavel, request.EmailResponsavel, request.TelefoneResponsavel, PlanoCodigo = request.PlanoCodigo?.ToUpperInvariant(), ModulosInteresse = JsonSerializer.Serialize(request.ModulosInteresse), request.UsuariosEstimados, request.EntidadesEstimadas, request.DesejaWhiteLabel, request.DesejaDominioCustomizado, request.DominioDesejado, CorrelationId = correlationId }, cancellationToken: cancellationToken)).ConfigureAwait(false);
        return (await GetAdminAsync(id, cancellationToken).ConfigureAwait(false))!;
    }

    public async Task<IReadOnlyCollection<SaasSolicitacaoClienteResponse>> ListAdminAsync(int offset, int limit, CancellationToken cancellationToken)
    {
        const string sql = SelectSql + " order by created_at desc offset @Offset limit @Limit;";
        using var connection = _context.CreateConnection();
        var rows = await connection.QueryAsync<SolicitacaoRow>(new CommandDefinition(sql, new { Offset = offset, Limit = limit }, cancellationToken: cancellationToken)).ConfigureAwait(false);
        return rows.Select(ToResponse).ToArray();
    }

    public async Task<SaasSolicitacaoClienteResponse?> GetAdminAsync(long id, CancellationToken cancellationToken)
    {
        const string sql = SelectSql + " where id=@Id limit 1;";
        using var connection = _context.CreateConnection();
        var row = await connection.QuerySingleOrDefaultAsync<SolicitacaoRow>(new CommandDefinition(sql, new { Id = id }, cancellationToken: cancellationToken)).ConfigureAwait(false);
        return row is null ? null : ToResponse(row);
    }

    public async Task UpdateStatusAsync(long id, string status, string? observacao, long? tenantId, Guid correlationId, CancellationToken cancellationToken)
    {
        const string sql = "update sigov.saas_solicitacao_cliente set status=@Status, observacao=@Observacao, tenant_id=coalesce(@TenantId, tenant_id), updated_at=now(), correlation_id=@CorrelationId where id=@Id;";
        using var connection = _context.CreateConnection();
        await connection.ExecuteAsync(new CommandDefinition(sql, new { Id = id, Status = status, Observacao = observacao, TenantId = tenantId, CorrelationId = correlationId }, cancellationToken: cancellationToken)).ConfigureAwait(false);
    }

    public async Task InsertEventoAsync(long? tenantId, string tipoEvento, string origem, long? origemId, object payload, Guid correlationId, CancellationToken cancellationToken)
    {
        const string sql = "insert into sigov.saas_evento (tenant_id,tipo_evento,origem,origem_id,payload,correlation_id) values (@TenantId,@TipoEvento,@Origem,@OrigemId,cast(@Payload as jsonb),@CorrelationId);";
        using var connection = _context.CreateConnection();
        await connection.ExecuteAsync(new CommandDefinition(sql, new { TenantId = tenantId, TipoEvento = tipoEvento, Origem = origem, OrigemId = origemId, Payload = JsonSerializer.Serialize(payload), CorrelationId = correlationId }, cancellationToken: cancellationToken)).ConfigureAwait(false);
    }

    private const string SelectSql = """
        select id as Id, protocolo as Protocolo, nome_organizacao as NomeOrganizacao, tipo_cliente as TipoCliente, documento as Documento, nome_responsavel as NomeResponsavel, email_responsavel as EmailResponsavel, telefone_responsavel as TelefoneResponsavel, plano_codigo as PlanoCodigo, usuarios_estimados as UsuariosEstimados, deseja_white_label as DesejaWhiteLabel, deseja_dominio_customizado as DesejaDominioCustomizado, status as Status, tenant_id as TenantId, created_at as CreatedAt from sigov.saas_solicitacao_cliente
        """;

    private static SaasSolicitacaoClienteResponse ToResponse(SolicitacaoRow row) => new(row.Id, row.Protocolo, row.NomeOrganizacao, row.TipoCliente, SaasSolicitacaoClienteMapper.MaskDocument(row.Documento), row.NomeResponsavel, SaasSolicitacaoClienteMapper.MaskEmail(row.EmailResponsavel) ?? string.Empty, SaasSolicitacaoClienteMapper.MaskDocument(row.TelefoneResponsavel), row.PlanoCodigo, row.UsuariosEstimados, row.DesejaWhiteLabel, row.DesejaDominioCustomizado, row.Status, row.TenantId, row.CreatedAt);

    private sealed record SolicitacaoRow(long Id, string Protocolo, string NomeOrganizacao, string TipoCliente, string? Documento, string NomeResponsavel, string EmailResponsavel, string? TelefoneResponsavel, string? PlanoCodigo, int? UsuariosEstimados, bool DesejaWhiteLabel, bool DesejaDominioCustomizado, string Status, long? TenantId, DateTimeOffset CreatedAt);
}

public sealed class SaasTenantProvisioningRepository : ISaasTenantProvisioningRepository
{
    private readonly DapperContext _context;
    public SaasTenantProvisioningRepository(DapperContext context) => _context = context;

    public async Task<long> ConverterSolicitacaoAsync(long solicitacaoId, ConverterSolicitacaoEmTenantRequest request, long usuarioId, Guid correlationId, CancellationToken cancellationToken)
    {
        using var connection = _context.CreateConnection();
        var solicitacao = await connection.QuerySingleAsync<SolicitacaoProvisioningRow>(new CommandDefinition("select id as Id, nome_organizacao as NomeOrganizacao, documento as Documento, email_responsavel as EmailResponsavel, plano_codigo as PlanoCodigo, usuarios_estimados as UsuariosEstimados, deseja_dominio_customizado as DesejaDominioCustomizado from sigov.saas_solicitacao_cliente where id=@Id and status <> 'CONVERTIDA_TENANT';", new { Id = solicitacaoId }, cancellationToken: cancellationToken)).ConfigureAwait(false);
        var tenantId = await connection.ExecuteScalarAsync<long>(new CommandDefinition("insert into sigov.tenant (nome,nome_fantasia,documento,slug,status,ambiente,metadados,created_by,correlation_id) values (@Nome,@Nome,@Documento,@Slug,'IMPLANTACAO','PRODUCTION',cast(@Metadados as jsonb),@UsuarioId,@CorrelationId) returning id;", new { Nome = solicitacao.NomeOrganizacao, solicitacao.Documento, request.SlugTenant, Metadados = JsonSerializer.Serialize(new { solicitacaoId }), UsuarioId = usuarioId, CorrelationId = correlationId }, cancellationToken: cancellationToken)).ConfigureAwait(false);
        var planoId = await connection.ExecuteScalarAsync<long>(new CommandDefinition("select id from sigov.saas_plano where codigo=coalesce(@PlanoCodigo,'ESSENCIAL') limit 1;", new { solicitacao.PlanoCodigo }, cancellationToken: cancellationToken)).ConfigureAwait(false);
        var assinaturaId = await connection.ExecuteScalarAsync<long>(new CommandDefinition("insert into sigov.saas_assinatura (tenant_id,plano_id,status,usuarios_contratados,periodicidade,created_by,correlation_id) values (@TenantId,@PlanoId,'EM_IMPLANTACAO',greatest(coalesce(@Usuarios,1),1),'MENSAL',@UsuarioId,@CorrelationId) returning id;", new { TenantId = tenantId, PlanoId = planoId, Usuarios = solicitacao.UsuariosEstimados, UsuarioId = usuarioId, CorrelationId = correlationId }, cancellationToken: cancellationToken)).ConfigureAwait(false);
        await connection.ExecuteAsync(new CommandDefinition("insert into sigov.saas_assinatura_modulo (tenant_id,assinatura_id,modulo_codigo,status) select @TenantId,@AssinaturaId,modulo_codigo,'EM_IMPLANTACAO' from sigov.saas_plano_modulo where plano_id=@PlanoId and incluso=true on conflict do nothing;", new { TenantId = tenantId, AssinaturaId = assinaturaId, PlanoId = planoId }, cancellationToken: cancellationToken)).ConfigureAwait(false);
        await connection.ExecuteAsync(new CommandDefinition("insert into sigov.saas_tenant_branding (tenant_id,nome_exibicao,white_label_ativo) values (@TenantId,@Nome,false) on conflict (tenant_id) do nothing;", new { TenantId = tenantId, Nome = solicitacao.NomeOrganizacao }, cancellationToken: cancellationToken)).ConfigureAwait(false);
        if (solicitacao.DesejaDominioCustomizado)
        {
            await connection.ExecuteAsync(new CommandDefinition("insert into sigov.saas_tenant_parametro_inicial (tenant_id,chave,valor_json) values (@TenantId,'dominio_customizado',cast(@Valor as jsonb)) on conflict do nothing;", new { TenantId = tenantId, Valor = JsonSerializer.Serialize(new { solicitado = true }) }, cancellationToken: cancellationToken)).ConfigureAwait(false);
        }
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes($"{request.EmailAdministrador}:{correlationId}")));
        await connection.ExecuteAsync(new CommandDefinition("insert into sigov.usuario (tenant_id,login,email,senha_hash,tipo_usuario,ativo,created_by,correlation_id) values (@TenantId,@Email,@Email,@Hash,'ADMINISTRADOR_TENANT',true,@UsuarioId,@CorrelationId) on conflict do nothing;", new { TenantId = tenantId, Email = request.EmailAdministrador, Hash = hash, UsuarioId = usuarioId, CorrelationId = correlationId }, cancellationToken: cancellationToken)).ConfigureAwait(false);
        await CreatePerfisAsync(connection, tenantId, usuarioId, correlationId, cancellationToken).ConfigureAwait(false);
        var onboardingId = await connection.ExecuteScalarAsync<long>(new CommandDefinition("insert into sigov.saas_onboarding_cliente (tenant_id,solicitacao_id) values (@TenantId,@SolicitacaoId) on conflict (tenant_id) do update set updated_at=now() returning id;", new { TenantId = tenantId, SolicitacaoId = solicitacaoId }, cancellationToken: cancellationToken)).ConfigureAwait(false);
        await CreateOnboardingTasksAsync(connection, tenantId, onboardingId, cancellationToken).ConfigureAwait(false);
        await connection.ExecuteAsync(new CommandDefinition("update sigov.saas_solicitacao_cliente set status='CONVERTIDA_TENANT', tenant_id=@TenantId, updated_at=now(), correlation_id=@CorrelationId where id=@SolicitacaoId; insert into sigov.saas_evento (tenant_id,tipo_evento,origem,origem_id,payload,correlation_id) values (@TenantId,'SaasSolicitacaoClienteConvertidaTenant','saas_solicitacao_cliente',@SolicitacaoId,cast(@Payload as jsonb),@CorrelationId),(@TenantId,'SaasAssinaturaCriada','saas_assinatura',@AssinaturaId,cast(@Payload as jsonb),@CorrelationId),(@TenantId,'OnboardingClienteCriado','saas_onboarding_cliente',@OnboardingId,cast(@Payload as jsonb),@CorrelationId),(@TenantId,'PerfisTenantCriados','perfil_acesso',@TenantId,cast(@Payload as jsonb),@CorrelationId);", new { TenantId = tenantId, SolicitacaoId = solicitacaoId, AssinaturaId = assinaturaId, OnboardingId = onboardingId, Payload = JsonSerializer.Serialize(new { tenantId, solicitacaoId, assinaturaId }), CorrelationId = correlationId }, cancellationToken: cancellationToken)).ConfigureAwait(false);
        return tenantId;
    }

    private static async Task CreatePerfisAsync(System.Data.IDbConnection connection, long tenantId, long usuarioId, Guid correlationId, CancellationToken cancellationToken)
    {
        const string sql = """
            insert into sigov.perfil_acesso (tenant_id,nome,descricao,codigo_externo,created_by,correlation_id)
            select @TenantId, nome, descricao, codigo, @UsuarioId, @CorrelationId from sigov.saas_perfil_template where ativo=true and nivel_base <> 'ADMINISTRADOR_GERAL'
            on conflict do nothing;
            """;
        await connection.ExecuteAsync(new CommandDefinition(sql, new { TenantId = tenantId, UsuarioId = usuarioId, CorrelationId = correlationId }, cancellationToken: cancellationToken)).ConfigureAwait(false);
    }

    private static async Task CreateOnboardingTasksAsync(System.Data.IDbConnection connection, long tenantId, long onboardingId, CancellationToken cancellationToken)
    {
        const string sql = """
            insert into sigov.saas_onboarding_tarefa (tenant_id,onboarding_id,codigo,nome,ordem,obrigatoria)
            select @TenantId,@OnboardingId,codigo,nome,ordem,true from (values
            ('confirmar_dados','Confirmar dados da organização',10),('configurar_branding','Configurar branding',20),('configurar_dominio','Configurar domínio, se contratado',30),('criar_usuarios','Criar usuários',40),('revisar_perfis','Revisar perfis',50),('conferir_modulos','Conferir módulos contratados',60),('configurar_entidade','Configurar entidade',70),('configurar_exercicio','Configurar exercício',80),('parametrizar_modulos','Parametrizar módulos',90),('checklist_implantacao','Rodar checklist de implantação',100),('aceitar_termo_implantacao','Aceitar termo de implantação',110)) as t(codigo,nome,ordem)
            on conflict do nothing;
            """;
        await connection.ExecuteAsync(new CommandDefinition(sql, new { TenantId = tenantId, OnboardingId = onboardingId }, cancellationToken: cancellationToken)).ConfigureAwait(false);
    }

    private sealed record SolicitacaoProvisioningRow(long Id, string NomeOrganizacao, string? Documento, string EmailResponsavel, string? PlanoCodigo, int? UsuariosEstimados, bool DesejaDominioCustomizado);
}
