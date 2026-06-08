using System.Security.Cryptography;
using System.Text;
using Dapper;
using Microsoft.Extensions.Logging;
using Sigov.Application.Saas.B2B;
using Sigov.Infrastructure.Persistence.Dapper;

namespace Sigov.Infrastructure.Saas.B2B;

public sealed class WhiteLabelB2BLaunchService : IWhiteLabelB2BLaunchService
{
    private readonly DapperContext _context;
    private readonly ILogger<WhiteLabelB2BLaunchService> _logger;

    public WhiteLabelB2BLaunchService(DapperContext context, ILogger<WhiteLabelB2BLaunchService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<IReadOnlyCollection<B2BPlanoDto>> GetPlanosPublicosAsync(CancellationToken cancellationToken)
    {
        const string sql = "select id as Id, codigo as Codigo, nome as Nome, coalesce(descricao, '') as Descricao, valor_mensal as ValorMensal, coalesce(publico_alvo, '') as PublicoAlvo, permite_white_label as PermiteWhiteLabel, permite_api as PermiteApi, limite_usuarios as LimiteUsuarios, limite_medicos as LimiteMedicos, limite_hospitais as LimiteHospitais, limite_plantoes_mes as LimitePlantoesMes, coalesce(sla_resumo, '') as SlaResumo from sigov.b2b_planos where ativo = true and publico = true order by ordem, valor_mensal;";
        using var connection = _context.CreateConnection();
        var rows = await connection.QueryAsync<B2BPlanoDto>(new CommandDefinition(sql, cancellationToken: cancellationToken)).ConfigureAwait(false);
        return rows.AsList();
    }

    public Task<IReadOnlyCollection<B2BPlanoComparativoDto>> GetComparativoAsync(CancellationToken cancellationToken)
    {
        IReadOnlyCollection<B2BPlanoComparativoDto> rows = new List<B2BPlanoComparativoDto>
        {
            new B2BPlanoComparativoDto("Médicos", "20", "100", "Customizável", "Multi-cliente", "Sob proposta"),
            new B2BPlanoComparativoDto("Unidades", "2", "10", "Customizável", "Tenants vinculados", "Sob proposta"),
            new B2BPlanoComparativoDto("Plantões/mês", "100", "500", "Customizável", "Por cliente final", "Sob proposta"),
            new B2BPlanoComparativoDto("White label", "Não", "Básico", "Completo", "Parceiro + clientes", "Custom"),
            new B2BPlanoComparativoDto("API", "Não", "Limitada", "Completa + webhooks", "Completa", "Custom"),
            new B2BPlanoComparativoDto("SLA", "Padrão", "Prioritário", "Contratual", "B2B", "Custom")
        };
        return Task.FromResult(rows);
    }

    public async Task<SelfServiceCadastroResult> SolicitarCadastroAsync(SelfServiceCadastroRequest request, string? ip, string? userAgent, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.RazaoSocial) || string.IsNullOrWhiteSpace(request.Cnpj) || string.IsNullOrWhiteSpace(request.ResponsavelEmail) || string.IsNullOrWhiteSpace(request.PlanoCodigo))
        {
            return new SelfServiceCadastroResult(false, null, "Razão social, CNPJ, e-mail responsável e plano são obrigatórios.");
        }

        if (!request.AceiteTermos || !request.AceiteLgpd)
        {
            return new SelfServiceCadastroResult(false, null, "É necessário aceitar os termos comerciais e a política LGPD.");
        }

        using var connection = _context.CreateConnection();
        connection.Open();
        using var transaction = connection.BeginTransaction();
        try
        {
            var planoId = await connection.ExecuteScalarAsync<long?>(new CommandDefinition("select id from sigov.b2b_planos where codigo = @PlanoCodigo and ativo = true limit 1;", new { request.PlanoCodigo }, transaction, cancellationToken: cancellationToken)).ConfigureAwait(false);
            if (!planoId.HasValue)
            {
                transaction.Rollback();
                return new SelfServiceCadastroResult(false, null, "Plano selecionado não está disponível.");
            }

            var duplicado = await connection.ExecuteScalarAsync<bool>(new CommandDefinition("select exists (select 1 from sigov.b2b_cadastro_cliente_solicitacoes where cnpj = @Cnpj and status not in ('CANCELADO','REPROVADO'));", new { request.Cnpj }, transaction, cancellationToken: cancellationToken)).ConfigureAwait(false);
            if (duplicado)
            {
                transaction.Rollback();
                return new SelfServiceCadastroResult(false, null, "Já existe cadastro em andamento para este CNPJ.");
            }

            var solicitacaoId = await connection.ExecuteScalarAsync<long>(new CommandDefinition("insert into sigov.b2b_cadastro_cliente_solicitacoes (razao_social, nome_fantasia, cnpj, responsavel_nome, responsavel_email, responsavel_telefone, plano_id, status, ip_origem, user_agent) values (@RazaoSocial, @NomeFantasia, @Cnpj, @ResponsavelNome, @ResponsavelEmail, @ResponsavelTelefone, @PlanoId, 'AGUARDANDO_VALIDACAO', @Ip, @UserAgent) returning id;", new { request.RazaoSocial, request.NomeFantasia, request.Cnpj, request.ResponsavelNome, request.ResponsavelEmail, request.ResponsavelTelefone, PlanoId = planoId.Value, Ip = Mask(ip), UserAgent = Mask(userAgent) }, transaction, cancellationToken: cancellationToken)).ConfigureAwait(false);

            await connection.ExecuteAsync(new CommandDefinition("insert into sigov.b2b_cadastro_cliente_aceites (solicitacao_id, tipo, versao, aceito, ip_origem, user_agent) values (@SolicitacaoId, 'TERMOS_COMERCIAIS', '2026.06', true, @Ip, @UserAgent), (@SolicitacaoId, 'LGPD', '2026.06', true, @Ip, @UserAgent);", new { SolicitacaoId = solicitacaoId, Ip = Mask(ip), UserAgent = Mask(userAgent) }, transaction, cancellationToken: cancellationToken)).ConfigureAwait(false);
            await RegistrarEventoAsync(connection, transaction, null, "SELF_SERVICE_CADASTRO_SOLICITADO", "b2b_cadastro_cliente_solicitacoes", solicitacaoId, cancellationToken).ConfigureAwait(false);
            transaction.Commit();
            return new SelfServiceCadastroResult(true, solicitacaoId, "Cadastro recebido. O onboarding automático seguirá as etapas de validação, provisionamento do tenant e criação do admin cliente.");
        }
        catch (Exception ex)
        {
            transaction.Rollback();
            _logger.LogError(ex, "Falha ao solicitar cadastro self-service B2B. CNPJ mascarado={Cnpj}", Mask(request.Cnpj));
            throw;
        }
    }

    public async Task<WhiteLabelConfiguracaoDto> GetWhiteLabelAsync(long tenantId, CancellationToken cancellationToken)
    {
        const string sql = "select tenant_id as TenantId, coalesce(nome_plataforma, 'PlantãoPro') as NomePlataforma, coalesce(nome_comercial, 'PlantãoPro') as NomeComercial, coalesce(logo_principal_url, '/img/logo.svg') as LogoPrincipalUrl, coalesce(logo_reduzida_url, '/img/logo.svg') as LogoReduzidaUrl, coalesce(favicon_url, '/favicon.ico') as FaviconUrl, coalesce(banner_login_url, '') as BannerLoginUrl, coalesce(cor_primaria, '#2563eb') as CorPrimaria, coalesce(cor_secundaria, '#0f172a') as CorSecundaria, coalesce(cor_destaque, '#22c55e') as CorDestaque, coalesce(cor_menu, '#111827') as CorMenu, coalesce(cor_fundo, '#f8fafc') as CorFundo, coalesce(tema, 'claro') as Tema, coalesce(slogan, '') as Slogan, coalesce(texto_boas_vindas, '') as TextoBoasVindas, coalesce(texto_rodape, '') as TextoRodape, coalesce(dominio_customizado, '') as DominioCustomizado, coalesce(subdominio, '') as Subdominio, coalesce(email_remetente, '') as EmailRemetente, publicado as Publicado from sigov.b2b_tenant_white_label where tenant_id = @TenantId;";
        using var connection = _context.CreateConnection();
        var row = await connection.QuerySingleOrDefaultAsync<WhiteLabelConfiguracaoDto>(new CommandDefinition(sql, new { TenantId = tenantId }, cancellationToken: cancellationToken)).ConfigureAwait(false);
        if (row is not null)
        {
            return row;
        }

        return new WhiteLabelConfiguracaoDto(tenantId, "PlantãoPro", "PlantãoPro", "/img/logo.svg", "/img/logo.svg", "/favicon.ico", string.Empty, "#2563eb", "#0f172a", "#22c55e", "#111827", "#f8fafc", "claro", "Gestão inteligente de plantões", "Bem-vindo ao seu portal de plantões.", "PlantãoPro", string.Empty, string.Empty, string.Empty, false);
    }

    public async Task<WhiteLabelConfiguracaoDto> AtualizarWhiteLabelAsync(long tenantId, WhiteLabelAtualizarRequest request, long? usuarioId, CancellationToken cancellationToken)
    {
        ValidarWhiteLabel(request);
        using var connection = _context.CreateConnection();
        connection.Open();
        using var transaction = connection.BeginTransaction();
        try
        {
            await connection.ExecuteAsync(new CommandDefinition("insert into sigov.b2b_tenant_white_label (tenant_id, nome_plataforma, nome_comercial, cor_primaria, cor_secundaria, cor_destaque, cor_menu, cor_fundo, tema, slogan, texto_boas_vindas, texto_rodape, dominio_customizado, subdominio, email_remetente, updated_by, updated_at) values (@TenantId, @NomePlataforma, @NomeComercial, @CorPrimaria, @CorSecundaria, @CorDestaque, @CorMenu, @CorFundo, @Tema, @Slogan, @TextoBoasVindas, @TextoRodape, @DominioCustomizado, @Subdominio, @EmailRemetente, @UsuarioId, now()) on conflict (tenant_id) do update set nome_plataforma = excluded.nome_plataforma, nome_comercial = excluded.nome_comercial, cor_primaria = excluded.cor_primaria, cor_secundaria = excluded.cor_secundaria, cor_destaque = excluded.cor_destaque, cor_menu = excluded.cor_menu, cor_fundo = excluded.cor_fundo, tema = excluded.tema, slogan = excluded.slogan, texto_boas_vindas = excluded.texto_boas_vindas, texto_rodape = excluded.texto_rodape, dominio_customizado = excluded.dominio_customizado, subdominio = excluded.subdominio, email_remetente = excluded.email_remetente, updated_by = excluded.updated_by, updated_at = now();", new { TenantId = tenantId, request.NomePlataforma, request.NomeComercial, request.CorPrimaria, request.CorSecundaria, request.CorDestaque, request.CorMenu, request.CorFundo, request.Tema, request.Slogan, request.TextoBoasVindas, request.TextoRodape, request.DominioCustomizado, request.Subdominio, request.EmailRemetente, UsuarioId = usuarioId }, transaction, cancellationToken: cancellationToken)).ConfigureAwait(false);
            await RegistrarEventoAsync(connection, transaction, tenantId, "WHITE_LABEL_ATUALIZADO", "b2b_tenant_white_label", tenantId, cancellationToken).ConfigureAwait(false);
            transaction.Commit();
            return await GetWhiteLabelAsync(tenantId, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            transaction.Rollback();
            _logger.LogError(ex, "Falha ao atualizar white label. TenantId={TenantId}", tenantId);
            throw;
        }
    }

    public Task PublicarWhiteLabelAsync(long tenantId, long? usuarioId, CancellationToken cancellationToken) => AlterarPublicacaoWhiteLabelAsync(tenantId, true, "WHITE_LABEL_PUBLICADO", usuarioId, cancellationToken);

    public Task RestaurarWhiteLabelPadraoAsync(long tenantId, long? usuarioId, CancellationToken cancellationToken) => AlterarPublicacaoWhiteLabelAsync(tenantId, false, "WHITE_LABEL_RESTAURADO_PADRAO", usuarioId, cancellationToken);

    public Task<DeveloperOverviewDto> GetDeveloperOverviewAsync(long tenantId, CancellationToken cancellationToken)
    {
        IReadOnlyCollection<string> escopos = new List<string> { "plantoes.read", "plantoes.write", "medicos.read", "webhooks.write", "relatorios.read" };
        IReadOnlyCollection<string> endpoints = new List<string> { "GET /api/white-label/mobile/config", "GET /api/planos/publicos", "GET /api/minha-assinatura/uso", "POST /api/developer/api-keys", "POST /api/suporte/chamados" };
        IReadOnlyCollection<string> eventos = new List<string> { "plantao.publicado", "convite.aceito", "pagamento.gerado", "sla.incidente", "limite.plano.atingido" };
        return Task.FromResult(new DeveloperOverviewDto("Bearer JWT para usuários e X-Api-Key para integrações servidor-servidor. A chave é exibida apenas uma vez e armazenada somente como hash SHA-256.", escopos, endpoints, "Rate limit por tenant/plano em janelas de minuto e mês.", eventos));
    }

    public async Task<ApiKeyCreateResult> CriarApiKeyAsync(long tenantId, ApiKeyCreateRequest request, long? usuarioId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Nome) || request.Escopos.Count == 0)
        {
            throw new InvalidOperationException("Nome e pelo menos um escopo são obrigatórios.");
        }

        var secret = "pp_" + Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)).Replace("+", "", StringComparison.Ordinal).Replace("/", "", StringComparison.Ordinal).Replace("=", "", StringComparison.Ordinal);
        var prefixo = secret.Substring(0, Math.Min(10, secret.Length));
        var hash = Hash(secret);
        using var connection = _context.CreateConnection();
        connection.Open();
        using var transaction = connection.BeginTransaction();
        try
        {
            var id = await connection.ExecuteScalarAsync<long>(new CommandDefinition("insert into sigov.b2b_api_chaves (tenant_id, nome, prefixo, api_key_hash, escopos, status, created_by) values (@TenantId, @Nome, @Prefixo, @Hash, @Escopos, 'ATIVA', @UsuarioId) returning id;", new { TenantId = tenantId, request.Nome, Prefixo = prefixo, Hash = hash, Escopos = string.Join(",", request.Escopos), UsuarioId = usuarioId }, transaction, cancellationToken: cancellationToken)).ConfigureAwait(false);
            await RegistrarEventoAsync(connection, transaction, tenantId, "API_KEY_CRIADA", "b2b_api_chaves", id, cancellationToken).ConfigureAwait(false);
            transaction.Commit();
            return new ApiKeyCreateResult(id, request.Nome, prefixo, secret, request.Escopos);
        }
        catch (Exception ex)
        {
            transaction.Rollback();
            _logger.LogError(ex, "Falha ao criar API key. TenantId={TenantId} Nome={Nome}", tenantId, request.Nome);
            throw;
        }
    }

    public async Task RevogarApiKeyAsync(long tenantId, long apiKeyId, long? usuarioId, CancellationToken cancellationToken)
    {
        using var connection = _context.CreateConnection();
        connection.Open();
        using var transaction = connection.BeginTransaction();
        try
        {
            await connection.ExecuteAsync(new CommandDefinition("update sigov.b2b_api_chaves set status = 'REVOGADA', revoked_at = now(), revoked_by = @UsuarioId where tenant_id = @TenantId and id = @ApiKeyId;", new { TenantId = tenantId, ApiKeyId = apiKeyId, UsuarioId = usuarioId }, transaction, cancellationToken: cancellationToken)).ConfigureAwait(false);
            await RegistrarEventoAsync(connection, transaction, tenantId, "API_KEY_REVOGADA", "b2b_api_chaves", apiKeyId, cancellationToken).ConfigureAwait(false);
            transaction.Commit();
        }
        catch (Exception ex)
        {
            transaction.Rollback();
            _logger.LogError(ex, "Falha ao revogar API key. TenantId={TenantId} ApiKeyId={ApiKeyId}", tenantId, apiKeyId);
            throw;
        }
    }

    public async Task<AssinaturaUsoDto> GetUsoAssinaturaAsync(long tenantId, CancellationToken cancellationToken)
    {
        const string sql = "select @TenantId as TenantId, coalesce(p.codigo, 'ESSENCIAL') as PlanoCodigo, coalesce(u.usuarios_ativos, 0)::bigint as UsuariosAtivos, coalesce(u.medicos_ativos, 0)::bigint as MedicosAtivos, coalesce(u.hospitais_ativos, 0)::bigint as HospitaisAtivos, coalesce(u.plantoes_mes, 0)::bigint as PlantoesMes, coalesce(u.requisicoes_api_mes, 0)::bigint as RequisicoesApiMes, coalesce(u.armazenamento_gb, 0)::numeric as ArmazenamentoGb from sigov.tenant t left join sigov.b2b_assinaturas a on a.tenant_id = t.id and a.status = 'ATIVA' left join sigov.b2b_planos p on p.id = a.plano_id left join sigov.b2b_tenant_uso u on u.tenant_id = t.id where t.id = @TenantId limit 1;";
        using var connection = _context.CreateConnection();
        var row = await connection.QuerySingleOrDefaultAsync<AssinaturaUsoDto>(new CommandDefinition(sql, new { TenantId = tenantId }, cancellationToken: cancellationToken)).ConfigureAwait(false);
        return row ?? new AssinaturaUsoDto(tenantId, "ESSENCIAL", 0, 0, 0, 0, 0, 0m);
    }

    public Task<long> SolicitarUpgradeAsync(long tenantId, AssinaturaSolicitacaoRequest request, long? usuarioId, CancellationToken cancellationToken) => InserirSolicitacaoPlanoAsync(tenantId, "UPGRADE", request, usuarioId, cancellationToken);

    public Task<long> SolicitarDowngradeAsync(long tenantId, AssinaturaSolicitacaoRequest request, long? usuarioId, CancellationToken cancellationToken) => InserirSolicitacaoPlanoAsync(tenantId, "DOWNGRADE", request, usuarioId, cancellationToken);

    public async Task<IReadOnlyCollection<ContratoSlaDto>> GetContratosAsync(long? tenantId, CancellationToken cancellationToken)
    {
        const string sql = "select c.id as Id, c.tenant_id as TenantId, coalesce(p.codigo, '') as PlanoCodigo, c.status as Status, c.inicio_vigencia as InicioVigencia, c.fim_vigencia as FimVigencia, c.valor_mensal as ValorMensal, c.taxa_setup as TaxaSetup, coalesce(c.uptime_contratado, '') as UptimeContratado, coalesce(c.tempo_resposta_suporte, '') as TempoRespostaSuporte, coalesce(c.tempo_resolucao_critico, '') as TempoResolucaoCritico, coalesce(c.propriedade_dados, '') as PropriedadeDados from sigov.b2b_contratos c left join sigov.b2b_planos p on p.id = c.plano_id where (@TenantId is null or c.tenant_id = @TenantId) order by c.created_at desc limit 100;";
        using var connection = _context.CreateConnection();
        var rows = await connection.QueryAsync<ContratoSlaDto>(new CommandDefinition(sql, new { TenantId = tenantId }, cancellationToken: cancellationToken)).ConfigureAwait(false);
        return rows.AsList();
    }

    public async Task<long> AbrirChamadoAsync(long tenantId, SuporteChamadoRequest request, long? usuarioId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Titulo) || string.IsNullOrWhiteSpace(request.Descricao))
        {
            throw new InvalidOperationException("Título e descrição são obrigatórios.");
        }

        using var connection = _context.CreateConnection();
        connection.Open();
        using var transaction = connection.BeginTransaction();
        try
        {
            var id = await connection.ExecuteScalarAsync<long>(new CommandDefinition("insert into sigov.b2b_suporte_chamados (tenant_id, titulo, descricao, prioridade, canal, critico, status, created_by) values (@TenantId, @Titulo, @Descricao, @Prioridade, @Canal, @Critico, 'ABERTO', @UsuarioId) returning id;", new { TenantId = tenantId, request.Titulo, request.Descricao, request.Prioridade, request.Canal, request.Critico, UsuarioId = usuarioId }, transaction, cancellationToken: cancellationToken)).ConfigureAwait(false);
            await RegistrarEventoAsync(connection, transaction, tenantId, "SUPORTE_CHAMADO_ABERTO", "b2b_suporte_chamados", id, cancellationToken).ConfigureAwait(false);
            transaction.Commit();
            return id;
        }
        catch (Exception ex)
        {
            transaction.Rollback();
            _logger.LogError(ex, "Falha ao abrir chamado B2B. TenantId={TenantId}", tenantId);
            throw;
        }
    }

    public async Task<IReadOnlyCollection<SuporteChamadoDto>> GetChamadosAsync(long tenantId, CancellationToken cancellationToken)
    {
        const string sql = "select id as Id, tenant_id as TenantId, titulo as Titulo, prioridade as Prioridade, status as Status, created_at as CriadoEm, coalesce(sla_resumo, '') as SlaResumo from sigov.b2b_suporte_chamados where tenant_id = @TenantId order by created_at desc limit 100;";
        using var connection = _context.CreateConnection();
        var rows = await connection.QueryAsync<SuporteChamadoDto>(new CommandDefinition(sql, new { TenantId = tenantId }, cancellationToken: cancellationToken)).ConfigureAwait(false);
        return rows.AsList();
    }

    public async Task<MonitoramentoB2BDto> GetMonitoramentoAsync(CancellationToken cancellationToken)
    {
        using var connection = _context.CreateConnection();
        var row = await connection.QuerySingleOrDefaultAsync<MonitoramentoB2BDto>(new CommandDefinition("select (select count(*)::bigint from sigov.tenant where status = 'ATIVO' and is_deleted = false) as TenantsAtivos, (select count(*)::bigint from sigov.b2b_telemetria_alertas where status = 'ABERTO' and severidade = 'CRITICA') as AlertasCriticos, (select count(*)::bigint from sigov.b2b_telemetria_erros_criticos where resolvido = false) as ErrosCriticos, (select count(*)::bigint from sigov.b2b_telemetria_endpoint_performance where duracao_ms >= 2000 and created_at >= now() - interval '1 day') as EndpointsLentos, (select count(*)::bigint from sigov.b2b_suporte_chamados where status <> 'RESOLVIDO' and critico = true) as ChamadosCriticos, (select count(*)::bigint from sigov.b2b_sla_incidentes where status <> 'RESOLVIDO') as IncidentesSla, now() as ColetadoEm;", cancellationToken: cancellationToken)).ConfigureAwait(false);
        return row ?? new MonitoramentoB2BDto(0, 0, 0, 0, 0, 0, DateTimeOffset.UtcNow);
    }

    public async Task<IReadOnlyCollection<GoToMarketMaterialDto>> GetMateriaisGoToMarketAsync(string visibilidade, CancellationToken cancellationToken)
    {
        const string sql = "select id as Id, titulo as Titulo, tipo as Tipo, visibilidade as Visibilidade, coalesce(conteudo_resumo, '') as ConteudoResumo from sigov.b2b_marketing_materiais where ativo = true and (visibilidade = @Visibilidade or @Visibilidade = 'interno') order by tipo, titulo limit 100;";
        using var connection = _context.CreateConnection();
        var rows = await connection.QueryAsync<GoToMarketMaterialDto>(new CommandDefinition(sql, new { Visibilidade = visibilidade }, cancellationToken: cancellationToken)).ConfigureAwait(false);
        return rows.AsList();
    }

    public async Task<IReadOnlyCollection<BetaFeedbackDto>> GetBetaFeedbacksAsync(long? tenantId, CancellationToken cancellationToken)
    {
        const string sql = "select id as Id, tenant_id as TenantId, titulo as Titulo, severidade as Severidade, status as Status, satisfacao as Satisfacao, created_at as CriadoEm from sigov.b2b_beta_feedbacks where (@TenantId is null or tenant_id = @TenantId) order by created_at desc limit 100;";
        using var connection = _context.CreateConnection();
        var rows = await connection.QueryAsync<BetaFeedbackDto>(new CommandDefinition(sql, new { TenantId = tenantId }, cancellationToken: cancellationToken)).ConfigureAwait(false);
        return rows.AsList();
    }

    private async Task AlterarPublicacaoWhiteLabelAsync(long tenantId, bool publicar, string evento, long? usuarioId, CancellationToken cancellationToken)
    {
        using var connection = _context.CreateConnection();
        connection.Open();
        using var transaction = connection.BeginTransaction();
        try
        {
            if (publicar)
            {
                await connection.ExecuteAsync(new CommandDefinition("update sigov.b2b_tenant_white_label set publicado = true, publicado_at = now(), updated_by = @UsuarioId, updated_at = now() where tenant_id = @TenantId; insert into sigov.b2b_white_label_publicacoes (tenant_id, status, created_by) values (@TenantId, 'PUBLICADO', @UsuarioId);", new { TenantId = tenantId, UsuarioId = usuarioId }, transaction, cancellationToken: cancellationToken)).ConfigureAwait(false);
            }
            else
            {
                await connection.ExecuteAsync(new CommandDefinition("update sigov.b2b_tenant_white_label set nome_plataforma = 'PlantãoPro', nome_comercial = 'PlantãoPro', logo_principal_url = '/img/logo.svg', logo_reduzida_url = '/img/logo.svg', favicon_url = '/favicon.ico', banner_login_url = null, cor_primaria = '#2563eb', cor_secundaria = '#0f172a', cor_destaque = '#22c55e', cor_menu = '#111827', cor_fundo = '#f8fafc', tema = 'claro', publicado = false, updated_by = @UsuarioId, updated_at = now() where tenant_id = @TenantId;", new { TenantId = tenantId, UsuarioId = usuarioId }, transaction, cancellationToken: cancellationToken)).ConfigureAwait(false);
            }

            await RegistrarEventoAsync(connection, transaction, tenantId, evento, "b2b_tenant_white_label", tenantId, cancellationToken).ConfigureAwait(false);
            transaction.Commit();
        }
        catch (Exception ex)
        {
            transaction.Rollback();
            _logger.LogError(ex, "Falha ao alterar publicação white label. TenantId={TenantId}", tenantId);
            throw;
        }
    }

    private async Task<long> InserirSolicitacaoPlanoAsync(long tenantId, string tipo, AssinaturaSolicitacaoRequest request, long? usuarioId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.PlanoDestinoCodigo) || string.IsNullOrWhiteSpace(request.Motivo))
        {
            throw new InvalidOperationException("Plano destino e motivo são obrigatórios.");
        }

        using var connection = _context.CreateConnection();
        connection.Open();
        using var transaction = connection.BeginTransaction();
        try
        {
            var id = await connection.ExecuteScalarAsync<long>(new CommandDefinition("insert into sigov.b2b_solicitacoes_plano (tenant_id, tipo, plano_destino_codigo, motivo, status, created_by) values (@TenantId, @Tipo, @PlanoDestinoCodigo, @Motivo, 'ABERTO', @UsuarioId) returning id;", new { TenantId = tenantId, Tipo = tipo, request.PlanoDestinoCodigo, request.Motivo, UsuarioId = usuarioId }, transaction, cancellationToken: cancellationToken)).ConfigureAwait(false);
            await RegistrarEventoAsync(connection, transaction, tenantId, "ASSINATURA_" + tipo + "_SOLICITADA", "b2b_solicitacoes_plano", id, cancellationToken).ConfigureAwait(false);
            transaction.Commit();
            return id;
        }
        catch (Exception ex)
        {
            transaction.Rollback();
            _logger.LogError(ex, "Falha ao solicitar alteração de plano. TenantId={TenantId} Tipo={Tipo}", tenantId, tipo);
            throw;
        }
    }

    private static Task RegistrarEventoAsync(System.Data.IDbConnection connection, System.Data.IDbTransaction transaction, long? tenantId, string evento, string entidade, long entidadeId, CancellationToken cancellationToken)
    {
        return connection.ExecuteAsync(new CommandDefinition("insert into sigov.b2b_telemetria_eventos (tenant_id, tipo_evento, entidade, entidade_id, severidade) values (@TenantId, @Evento, @Entidade, @EntidadeId, 'INFO');", new { TenantId = tenantId, Evento = evento, Entidade = entidade, EntidadeId = entidadeId }, transaction, cancellationToken: cancellationToken));
    }

    private static void ValidarWhiteLabel(WhiteLabelAtualizarRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.NomePlataforma) || string.IsNullOrWhiteSpace(request.NomeComercial))
        {
            throw new InvalidOperationException("Nome da plataforma e nome comercial são obrigatórios.");
        }

        ValidarCor(request.CorPrimaria, "cor primária");
        ValidarCor(request.CorSecundaria, "cor secundária");
        ValidarCor(request.CorDestaque, "cor de destaque");
        ValidarCor(request.CorMenu, "cor do menu");
        ValidarCor(request.CorFundo, "cor de fundo");
    }

    private static void ValidarCor(string value, string nome)
    {
        if (string.IsNullOrWhiteSpace(value) || !value.StartsWith("#", StringComparison.Ordinal) || value.Length != 7)
        {
            throw new InvalidOperationException("A " + nome + " deve estar no formato hexadecimal #RRGGBB.");
        }
    }

    private static string Hash(string value)
    {
        using var sha = SHA256.Create();
        return Convert.ToHexString(sha.ComputeHash(Encoding.UTF8.GetBytes(value)));
    }

    private static string? Mask(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return value;
        }

        return value.Length <= 4 ? "****" : value.Substring(0, 2) + "***" + value.Substring(value.Length - 2, 2);
    }
}
