using Dapper;
using Sigov.Application.Abstractions;
using Sigov.Infrastructure.Persistence.Dapper;
using Sigov.Web.Models.PostBuild;
using Sigov.Web.Helpers;

namespace Sigov.Web.Services;

public sealed class PostBuildSaasService
{
    private static readonly IReadOnlyCollection<ModuleViewModel> DefaultModules = new[]
    {
        new ModuleViewModel("tributario", "Tributário", SigovFeatureStatus.Parcial, "Receitas, dívida ativa e arrecadação."),
        new ModuleViewModel("rh", "RH", SigovFeatureStatus.Parcial, "Pessoas, vínculos e folha."),
        new ModuleViewModel("juridico", "Jurídico", SigovFeatureStatus.EmImplantacao, "Processos e pareceres jurídicos."),
        new ModuleViewModel("contratos", "Contratos", SigovFeatureStatus.EmImplantacao, "Gestão contratual."),
        new ModuleViewModel("ged", "GED", SigovFeatureStatus.EmImplantacao, "Gestão eletrônica de documentos."),
        new ModuleViewModel("protocolo", "Protocolo", SigovFeatureStatus.EmImplantacao, "Atendimento e processos digitais."),
        new ModuleViewModel("saude", "Saúde", SigovFeatureStatus.Parcial, "Atenção básica e vigilância."),
        new ModuleViewModel("educacao", "Educação", SigovFeatureStatus.Parcial, "Escolas, matrículas e frequência."),
        new ModuleViewModel("agro", "Agro", SigovFeatureStatus.Parcial, "Produtores, propriedades e programas rurais."),
        new ModuleViewModel("saneamento", "Saneamento", SigovFeatureStatus.Parcial, "Serviços e indicadores de saneamento."),
        new ModuleViewModel("social", "Assistência Social", SigovFeatureStatus.Parcial, "Cadastros e atendimentos sociais."),
        new ModuleViewModel("integracoes", "Integrações", SigovFeatureStatus.EmImplantacao, "APIs, webhooks e conectores.")
    };

    private readonly NpgsqlConnectionFactory _connectionFactory;
    private readonly ILogger<PostBuildSaasService> _logger;
    private readonly IDatabaseSchemaInspector _schemaInspector;

    public PostBuildSaasService(NpgsqlConnectionFactory connectionFactory, ILogger<PostBuildSaasService> logger, IDatabaseSchemaInspector schemaInspector)
    {
        _connectionFactory = connectionFactory;
        _logger = logger;
        _schemaInspector = schemaInspector;
    }

    public async Task<IReadOnlyCollection<TenantListItemViewModel>> ListarTenantsAsync(string? busca, CancellationToken cancellationToken)
    {
        try
        {
            var columns = await _schemaInspector.GetColumnsAsync("sigov", "tenant", cancellationToken).ConfigureAwait(false);
            if (!columns.Contains("id") || !columns.Contains("nome")) return Array.Empty<TenantListItemViewModel>();

            var codigoExpr = columns.Contains("slug") ? "slug" : columns.Contains("codigo") ? "codigo" : "id::text";
            var documentoExpr = columns.Contains("documento") ? "coalesce(documento,'')" : "''";
            var emailExpr = columns.Contains("email") ? "coalesce(email,'')" : "''";
            var telefoneExpr = columns.Contains("telefone") ? "coalesce(telefone,'')" : "''";
            var planoExpr = columns.Contains("plano") ? "coalesce(plano,'global')" : columns.Contains("metadados") ? "coalesce(metadados->>'plano','global')" : "'global'";
            var ativoExpr = columns.Contains("ativo") ? "coalesce(ativo,true)" : "true";
            var deletedWhere = columns.Contains("is_deleted") ? "coalesce(is_deleted,false)=false" : "true";
            var buscaWhere = columns.Contains("slug")
                ? "and (@Busca is null or nome ilike '%' || @Busca || '%' or slug ilike '%' || @Busca || '%'" + (columns.Contains("documento") ? " or coalesce(documento,'') ilike '%' || @Busca || '%'" : string.Empty) + ")"
                : "and (@Busca is null or nome ilike '%' || @Busca || '%')";
            var sql = $@"select id, nome, {codigoExpr} as Codigo, {documentoExpr} as Documento, {emailExpr} as Email, {telefoneExpr} as Telefone, {planoExpr} as Plano, {ativoExpr} as Ativo
from sigov.tenant
where {deletedWhere} {buscaWhere}
order by nome
limit 50;";
            using var connection = _connectionFactory.CreateConnection();
            var rows = await connection.QueryAsync<TenantRow>(new CommandDefinition(sql, new { Busca = string.IsNullOrWhiteSpace(busca) ? null : busca.Trim() }, cancellationToken: cancellationToken)).ConfigureAwait(false);
            return rows.Select(t => new TenantListItemViewModel(t.Id, t.Nome, t.Codigo, MaskDocument(t.Documento), MaskEmail(t.Email), MaskPhone(t.Telefone), t.Plano, t.Ativo)).ToArray();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falha ao listar tenants para dashboard SaaS.");
            return Array.Empty<TenantListItemViewModel>();
        }
    }

    public async Task<IReadOnlyCollection<ModuleViewModel>> ListarModulosAsync(long? tenantId, CancellationToken cancellationToken)
    {
        const string sql = @"select modulo_codigo as Codigo, status
from sigov.tenant_modulo_contratado
where (@TenantId is null or tenant_id = @TenantId)
order by modulo_codigo;";
        try
        {
            if (!await _schemaInspector.TableExistsAsync("sigov", "tenant_modulo_contratado", cancellationToken).ConfigureAwait(false)) return DefaultModules;
            using var connection = _connectionFactory.CreateConnection();
            var rows = (await connection.QueryAsync<ModuleStatusRow>(new CommandDefinition(sql, new { TenantId = tenantId }, cancellationToken: cancellationToken)).ConfigureAwait(false)).ToDictionary(x => x.Codigo, x => x.Status, StringComparer.OrdinalIgnoreCase);
            return DefaultModules.Select(m => m with { Status = rows.TryGetValue(m.Codigo, out var status) ? NormalizeStatus(status) : m.Status }).ToArray();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falha ao listar módulos contratados.");
            return DefaultModules;
        }
    }

    public async Task<(bool Ok, string Mensagem, long? Id)> SalvarTenantAsync(TenantFormViewModel form, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(form.Nome)) return (false, "Nome é obrigatório.", form.Id);
        try
        {
            var columns = await _schemaInspector.GetColumnsAsync("sigov", "tenant", cancellationToken).ConfigureAwait(false);
            if (!columns.Contains("id") || !columns.Contains("nome")) return (false, "Tabela sigov.tenant indisponível ou sem colunas mínimas; tenant não foi persistido.", form.Id);
            var hasSlug = columns.Contains("slug");
            var hasCodigo = columns.Contains("codigo");
            var code = string.IsNullOrWhiteSpace(form.Slug) ? Slugify(form.Nome) : form.Slug.Trim();

            using var connection = _connectionFactory.CreateConnection();
            if (hasSlug || hasCodigo)
            {
                var keyColumn = hasSlug ? "slug" : "codigo";
                var deletedWhere = columns.Contains("is_deleted") ? " and coalesce(is_deleted,false)=false" : string.Empty;
                var duplicado = await connection.ExecuteScalarAsync<int>(new CommandDefinition($"select count(*) from sigov.tenant where lower({keyColumn})=lower(@Codigo) and (@Id is null or id<>@Id){deletedWhere};", new { form.Id, Codigo = code }, cancellationToken: cancellationToken)).ConfigureAwait(false);
                if (duplicado > 0) return (false, $"Já existe tenant com este {(hasSlug ? "slug" : "código") }.", form.Id);
            }

            var metadados = System.Text.Json.JsonSerializer.Serialize(new { form.Email, form.Telefone, form.Plano, form.Observacao, form.CorPrincipal, form.LogoUrl, form.Subdominio, form.EmailSuporte });
            var values = new Dictionary<string, object?> { ["Nome"] = form.Nome.Trim(), ["Codigo"] = code, ["Documento"] = form.Documento, ["Email"] = form.Email, ["Telefone"] = form.Telefone, ["Plano"] = form.Plano, ["CorPrincipal"] = form.CorPrincipal, ["LogoUrl"] = form.LogoUrl, ["Ativo"] = form.Ativo, ["Metadados"] = metadados, ["Id"] = form.Id };

            if (form.Id.HasValue)
            {
                var sets = new List<string> { "nome=@Nome" };
                AddSet(sets, columns, "slug", "@Codigo"); AddSet(sets, columns, "codigo", "@Codigo"); AddSet(sets, columns, "documento", "@Documento"); AddSet(sets, columns, "email", "@Email"); AddSet(sets, columns, "telefone", "@Telefone"); AddSet(sets, columns, "plano", "@Plano"); AddSet(sets, columns, "cor_primaria", "@CorPrincipal"); AddSet(sets, columns, "logo_url", "@LogoUrl"); AddSet(sets, columns, "ativo", "@Ativo");
                if (columns.Contains("status")) sets.Add("status=case when @Ativo then 'ATIVO' else 'INATIVO' end");
                if (columns.Contains("metadados")) sets.Add("metadados=@Metadados::jsonb");
                if (columns.Contains("updated_at")) sets.Add("updated_at=now()");
                var where = columns.Contains("is_deleted") ? "id=@Id and coalesce(is_deleted,false)=false" : "id=@Id";
                var rows = await connection.ExecuteAsync(new CommandDefinition($"update sigov.tenant set {string.Join(", ", sets)} where {where};", values, cancellationToken: cancellationToken)).ConfigureAwait(false);
                if (rows == 0) return (false, "Tenant não encontrado; nada foi persistido.", form.Id);
                await AuditarSaasAsync(connection, "SAAS_TENANT_EDITAR", "sigov.tenant", form.Id.Value, form, cancellationToken).ConfigureAwait(false);
                return (true, "Tenant atualizado com sucesso.", form.Id);
            }

            var cols = new List<string> { "nome" }; var vals = new List<string> { "@Nome" };
            AddInsert(cols, vals, columns, "slug", "@Codigo"); AddInsert(cols, vals, columns, "codigo", "@Codigo"); AddInsert(cols, vals, columns, "documento", "@Documento"); AddInsert(cols, vals, columns, "email", "@Email"); AddInsert(cols, vals, columns, "telefone", "@Telefone"); AddInsert(cols, vals, columns, "plano", "@Plano"); AddInsert(cols, vals, columns, "cor_primaria", "@CorPrincipal"); AddInsert(cols, vals, columns, "logo_url", "@LogoUrl"); AddInsert(cols, vals, columns, "ativo", "@Ativo");
            if (columns.Contains("status")) { cols.Add("status"); vals.Add("'ATIVO'"); }
            if (columns.Contains("ambiente")) { cols.Add("ambiente"); vals.Add("'PRODUCTION'"); }
            if (columns.Contains("metadados")) { cols.Add("metadados"); vals.Add("@Metadados::jsonb"); }
            if (columns.Contains("created_at")) { cols.Add("created_at"); vals.Add("now()"); }
            var id = await connection.ExecuteScalarAsync<long>(new CommandDefinition($"insert into sigov.tenant ({string.Join(",", cols)}) values ({string.Join(",", vals)}) returning id;", values, cancellationToken: cancellationToken)).ConfigureAwait(false);
            await AuditarSaasAsync(connection, "SAAS_TENANT_CRIAR", "sigov.tenant", id, form, cancellationToken).ConfigureAwait(false);
            return (true, "Tenant criado com sucesso.", id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falha ao salvar tenant real.");
            return (false, "Não foi possível persistir o tenant. Nenhum sucesso foi simulado.", form.Id);
        }
    }

    public async Task<bool> AlterarStatusTenantAsync(long id, bool ativo, CancellationToken cancellationToken)
    {
        if (id <= 0) return false;
        try
        {
            var columns = await _schemaInspector.GetColumnsAsync("sigov", "tenant", cancellationToken).ConfigureAwait(false);
            if (!columns.Contains("id")) return false;
            if (!columns.Contains("ativo") && !columns.Contains("status")) return false;
            using var connection = _connectionFactory.CreateConnection();
            var sets = new List<string>();
            if (columns.Contains("ativo")) sets.Add("ativo=@Ativo");
            if (columns.Contains("status")) sets.Add("status=case when @Ativo then 'ATIVO' else 'INATIVO' end");
            if (columns.Contains("updated_at")) sets.Add("updated_at=now()");
            var where = columns.Contains("is_deleted") ? "id=@Id and coalesce(is_deleted,false)=false" : "id=@Id";
            var rows = await connection.ExecuteAsync(new CommandDefinition($"update sigov.tenant set {string.Join(", ", sets)} where {where};", new { Id = id, Ativo = ativo }, cancellationToken: cancellationToken)).ConfigureAwait(false);
            if (rows > 0) await AuditarSaasAsync(connection, ativo ? "SAAS_TENANT_ATIVAR" : "SAAS_TENANT_INATIVAR", "sigov.tenant", id, new { id, ativo }, cancellationToken).ConfigureAwait(false);
            return rows > 0;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Falha ao alterar status do tenant; nenhuma alteração foi simulada. TenantId={TenantId}", id);
            return false;
        }
    }

    public async Task<bool> AlterarModuloTenantAsync(long tenantId, string codigo, bool ativo, CancellationToken cancellationToken)
    {
        if (tenantId <= 0 || string.IsNullOrWhiteSpace(codigo)) return false;
        try
        {
            if (!await _schemaInspector.TableExistsAsync("sigov", "tenant_modulo_contratado", cancellationToken).ConfigureAwait(false)) return false;
            using var connection = _connectionFactory.CreateConnection();
            var status = ativo ? "HABILITADO" : "SUSPENSO";
            var rows = await connection.ExecuteAsync(new CommandDefinition(@"insert into sigov.tenant_modulo_contratado (tenant_id, modulo_codigo, status, contratado_em, vigencia_inicio, ativo)
values (@TenantId, @Codigo, @Status, current_date, current_date, @Ativo)
on conflict (tenant_id, modulo_codigo) do update set status=excluded.status, ativo=excluded.ativo, updated_at=now();",
                new { TenantId = tenantId, Codigo = codigo.Trim().ToLowerInvariant(), Status = status, Ativo = ativo }, cancellationToken: cancellationToken)).ConfigureAwait(false);
            if (rows > 0) await AuditarSaasAsync(connection, ativo ? "SAAS_MODULO_ATIVAR" : "SAAS_MODULO_INATIVAR", "sigov.tenant_modulo_contratado", tenantId, new { tenantId, codigo, ativo }, cancellationToken).ConfigureAwait(false);
            return rows > 0;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Estrutura de módulos por tenant indisponível; alteração não persistida.");
            return false;
        }
    }

    public async Task<ParametrosSaasViewModel> ListarParametrosAsync(long tenantId, string? categoria, string? escopo, string? busca, CancellationToken cancellationToken)
    {
        try
        {
            var hasTenantParametro = false;
            var parametroColumns = await _schemaInspector.GetColumnsAsync("sigov", "parametro_sistema", cancellationToken).ConfigureAwait(false);
            var hasParametroSistema = parametroColumns.Contains("chave") && parametroColumns.Contains("valor");
            if (!hasTenantParametro && !hasParametroSistema)
            {
                return new ParametrosSaasViewModel { TenantId = tenantId, MensagemFallback = "Nenhuma tabela de parâmetros encontrada; não há edição simulada.", PodePersistir = false };
            }

            using var connection = _connectionFactory.CreateConnection();
            if (hasTenantParametro)
            {
                var rows = await connection.QueryAsync<ParametroRow>(new CommandDefinition(@"select id, chave, case when coalesce(sensivel,false) then '••••••' else coalesce(valor,'') end as valor, coalesce(tipo,'texto') as tipo, coalesce(descricao,'') as descricao, coalesce(sensivel,false) as sensivel
from sigov.tenant_parametro
where tenant_id=@TenantId
order by chave
limit 200;", new { TenantId = tenantId }, cancellationToken: cancellationToken)).ConfigureAwait(false);
                return new ParametrosSaasViewModel { TenantId = tenantId, Categoria = categoria ?? string.Empty, Escopo = escopo ?? string.Empty, Busca = busca ?? string.Empty, Parametros = rows.Select(ToViewModel).ToArray(), PodePersistir = true };
            }

            var hasIdColumn = parametroColumns.Contains("id");
            var hasTenantIdColumn = parametroColumns.Contains("tenant_id");
            var hasTipoColumn = parametroColumns.Contains("tipo");
            var hasDescricaoColumn = parametroColumns.Contains("descricao");
            var hasCategoriaColumn = parametroColumns.Contains("categoria");
            var hasEscopoColumn = parametroColumns.Contains("escopo");
            var hasSensivelColumn = parametroColumns.Contains("sensivel");
            var idSelect = hasIdColumn ? "id" : "0::bigint";
            var tipoSelect = hasTipoColumn ? "coalesce(tipo,'string')" : "'string'";
            var descricaoSelect = hasDescricaoColumn ? "coalesce(descricao,'')" : "''";
            var sensivelExpr = hasSensivelColumn
                ? "coalesce(sensivel,false) or lower(chave) like any(array['%senha%','%password%','%token%','%secret%','%chave%','%key%','%api_key%','%client_secret%','%certificado%'])"
                : "lower(chave) like any(array['%senha%','%password%','%token%','%secret%','%chave%','%key%','%api_key%','%client_secret%','%certificado%'])";
            var filtros = new List<string>();
            if (hasTenantIdColumn && tenantId > 0) filtros.Add("tenant_id=@TenantId");
            if (hasCategoriaColumn && !string.IsNullOrWhiteSpace(categoria)) filtros.Add("categoria=@Categoria");
            if (hasEscopoColumn && !string.IsNullOrWhiteSpace(escopo)) filtros.Add("escopo=@Escopo");
            if (!string.IsNullOrWhiteSpace(busca)) filtros.Add("(chave ilike '%' || @Busca || '%'" + (hasDescricaoColumn ? " or descricao ilike '%' || @Busca || '%'" : string.Empty) + ")");
            var where = filtros.Count > 0 ? " where " + string.Join(" and ", filtros) : string.Empty;
            var order = hasCategoriaColumn ? "categoria, chave" : "chave";
            var sql = $@"select {idSelect} as id, chave, case when {sensivelExpr} then '••••••' else coalesce(valor::text,'') end as valor, {tipoSelect} as tipo, {descricaoSelect} as descricao, {sensivelExpr} as sensivel from sigov.parametro_sistema{where} order by {order} limit 200;";
            var sistemaRows = await connection.QueryAsync<ParametroRow>(new CommandDefinition(sql, new { TenantId = tenantId, Categoria = categoria, Escopo = escopo, Busca = busca }, cancellationToken: cancellationToken)).ConfigureAwait(false);
            return new ParametrosSaasViewModel { TenantId = tenantId, Categoria = categoria ?? string.Empty, Escopo = escopo ?? string.Empty, Busca = busca ?? string.Empty, Parametros = sistemaRows.Select(ToViewModel).ToArray(), MensagemFallback = "Usando sigov.parametro_sistema em modo schema-safe.", PodePersistir = true };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falha ao listar parâmetros SaaS. TenantId={TenantId}", tenantId);
            return new ParametrosSaasViewModel { TenantId = tenantId, MensagemFallback = "Não foi possível consultar parâmetros agora.", PodePersistir = false };
        }
    }

    public async Task<(bool Ok, string Mensagem)> SalvarParametroAsync(ParametroSaasFormViewModel form, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(form.Chave)) return (false, "Chave do parâmetro é obrigatória.");
        var tipo = NormalizeParameterType(form.Tipo);
        var validation = ValidateParameterValue(tipo, form.Valor);
        if (!validation.Ok) return validation;

        try
        {
            var columns = await _schemaInspector.GetColumnsAsync("sigov", "parametro_sistema", cancellationToken).ConfigureAwait(false);
            if (!columns.Contains("chave") || !columns.Contains("valor")) return (false, "Tabela sigov.parametro_sistema indisponível ou sem colunas mínimas; parâmetro não foi persistido.");
            using var connection = _connectionFactory.CreateConnection();
            var sets = new List<string> { "valor=@Valor" };
            if (columns.Contains("tipo")) sets.Add("tipo=@Tipo");
            if (columns.Contains("descricao")) sets.Add("descricao=@Descricao");
            if (columns.Contains("sensivel")) sets.Add("sensivel=@Sensivel");
            if (columns.Contains("updated_at")) sets.Add("updated_at=now()");
            var where = new List<string> { "chave=@Chave" };
            if (columns.Contains("tenant_id")) where.Add("coalesce(tenant_id,0)=@TenantId");
            if (columns.Contains("escopo") && !string.IsNullOrWhiteSpace(form.Escopo)) where.Add("escopo=@Escopo");
            var rows = await connection.ExecuteAsync(new CommandDefinition($"update sigov.parametro_sistema set {string.Join(", ", sets)} where {string.Join(" and ", where)};", new { form.TenantId, Chave = form.Chave.Trim(), Valor = (form.Valor ?? string.Empty).Trim(), Tipo = tipo, form.Descricao, form.Sensivel, form.Escopo }, cancellationToken: cancellationToken)).ConfigureAwait(false);
            if (rows == 0) return (false, "Parâmetro não encontrado no schema real; nenhuma inclusão foi simulada.");
            await AuditarSaasAsync(connection, "SAAS_PARAMETRO_EDITAR", "sigov.parametro_sistema", form.Id ?? form.TenantId, new { form.TenantId, Chave = form.Chave, Tipo = tipo, Sensivel = form.Sensivel, Valor = form.Sensivel ? "***" : form.Valor }, cancellationToken).ConfigureAwait(false);
            return (true, "Parâmetro atualizado com validação por tipo e auditoria.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falha ao salvar parâmetro SaaS. TenantId={TenantId} Chave={Chave}", form.TenantId, form.Chave);
            return (false, "Não foi possível salvar o parâmetro. Nenhum sucesso foi simulado.");
        }
    }

    public async Task<(bool Ok, string Mensagem)> RestaurarParametroPadraoAsync(ParametroSaasFormViewModel form, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(form.Chave)) return (false, "Chave do parâmetro é obrigatória para restaurar padrão.");
        try
        {
            var columns = await _schemaInspector.GetColumnsAsync("sigov", "parametro_sistema", cancellationToken).ConfigureAwait(false);
            if (!columns.Contains("chave") || !columns.Contains("valor") || !columns.Contains("valor_padrao")) return (false, "Coluna valor_padrao indisponível; restauração não foi simulada.");
            using var connection = _connectionFactory.CreateConnection();
            var sets = new List<string> { "valor=valor_padrao" };
            if (columns.Contains("updated_at")) sets.Add("updated_at=now()");
            var where = new List<string> { "chave=@Chave" };
            if (columns.Contains("tenant_id")) where.Add("coalesce(tenant_id,0)=@TenantId");
            if (columns.Contains("escopo") && !string.IsNullOrWhiteSpace(form.Escopo)) where.Add("escopo=@Escopo");
            var rows = await connection.ExecuteAsync(new CommandDefinition($"update sigov.parametro_sistema set {string.Join(", ", sets)} where {string.Join(" and ", where)};", new { form.TenantId, Chave = form.Chave.Trim(), form.Escopo }, cancellationToken: cancellationToken)).ConfigureAwait(false);
            if (rows == 0) return (false, "Parâmetro não encontrado; restauração não foi simulada.");
            await AuditarSaasAsync(connection, "SAAS_PARAMETRO_RESTAURAR_PADRAO", "sigov.parametro_sistema", form.Id ?? form.TenantId, new { form.TenantId, form.Chave, form.Escopo }, cancellationToken).ConfigureAwait(false);
            return (true, "Valor padrão restaurado com auditoria.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falha ao restaurar valor padrão de parâmetro SaaS. Chave={Chave}", form.Chave);
            return (false, "Não foi possível restaurar o padrão. Nenhum sucesso foi simulado.");
        }
    }

    public async Task<bool> RegistrarOperacaoVisualAsync(string operacao, object payload, CancellationToken cancellationToken)
    {
        try
        {
            using var connection = _connectionFactory.CreateConnection();
            const string sql = @"insert into sigov.auditoria_evento (acao, entidade, depois, created_at)
values (@Acao, @Entidade, @Depois::jsonb, now());";
            var json = System.Text.Json.JsonSerializer.Serialize(payload);
            await connection.ExecuteAsync(new CommandDefinition(sql, new { Acao = operacao, Entidade = "sigov.saas", Depois = json }, cancellationToken: cancellationToken)).ConfigureAwait(false);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Operação SaaS registrada apenas como fallback visual. Operacao={Operacao}", operacao);
            return false;
        }
    }

    public async Task<DashboardViewModel> CriarDashboardAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var connection = _connectionFactory.CreateConnection();
            var tenantId = 1L;
            var protocolosAbertos = await CountOrNullAsync(connection, "select count(*) from sigov.protocolo where tenant_id=@TenantId and coalesce(is_deleted,false)=false and upper(status) in ('ABERTO','PENDENTE');", cancellationToken, new { TenantId = tenantId }).ConfigureAwait(false);
            var protocolosTramitacao = await CountOrNullAsync(connection, "select count(*) from sigov.protocolo where tenant_id=@TenantId and coalesce(is_deleted,false)=false and upper(status) in ('EM_TRAMITACAO','TRAMITANDO');", cancellationToken, new { TenantId = tenantId }).ConfigureAwait(false);
            var protocolosConcluidos = await CountOrNullAsync(connection, "select count(*) from sigov.protocolo where tenant_id=@TenantId and coalesce(is_deleted,false)=false and upper(status) in ('CONCLUIDO','ARQUIVADO');", cancellationToken, new { TenantId = tenantId }).ConfigureAwait(false);
            var tarefasPendentes = await CountOrNullAsync(connection, "select count(*) from sigov.tarefa where tenant_id=@TenantId and coalesce(is_deleted,false)=false and upper(status) in ('PENDENTE','ABERTA');", cancellationToken, new { TenantId = tenantId }).ConfigureAwait(false);
            var tarefasVencidas = await CountOrNullAsync(connection, "select count(*) from sigov.tarefa where tenant_id=@TenantId and coalesce(is_deleted,false)=false and upper(status)='VENCIDA';", cancellationToken, new { TenantId = tenantId }).ConfigureAwait(false);
            var notificacoesNaoLidas = await CountOrNullAsync(connection, "select count(*) from sigov.notificacao where tenant_id=@TenantId and coalesce(is_deleted,false)=false and upper(status) in ('NAO_LIDA','NOVA');", cancellationToken, new { TenantId = tenantId }).ConfigureAwait(false);
            var documentosCriados = await CountOrNullAsync(connection, "select count(*) from sigov.documento where tenant_id=@TenantId and coalesce(is_deleted,false)=false;", cancellationToken, new { TenantId = tenantId }).ConfigureAwait(false);
            var outboxProblemas = await CountOrNullAsync(connection, "select count(*) from sigov.outbox_evento where tenant_id=@TenantId and coalesce(is_deleted,false)=false and upper(status) in ('PENDENTE','FALHOU');", cancellationToken, new { TenantId = tenantId }).ConfigureAwait(false);
            var webhooksFalha = await CountOrNullAsync(connection, "select count(*) from sigov.webhook_entrega where tenant_id=@TenantId and upper(status) in ('FALHOU','ERRO');", cancellationToken, new { TenantId = tenantId }).ConfigureAwait(false);

            var protocolosPorStatus = await QueryOrEmptyAsync<DashboardStatusSliceViewModel>(connection, @"select status as Status, count(*) as Total from sigov.protocolo where tenant_id=@TenantId and coalesce(is_deleted,false)=false group by status order by 2 desc;", new { TenantId = tenantId }, cancellationToken).ConfigureAwait(false);
            var ultimosProtocolos = await QueryOrEmptyAsync<DashboardListItemViewModel>(connection, @"select numero as Titulo, assunto as Descricao, status as Status, '/Protocolo/Detalhe/' || id as Url, created_at as Data from sigov.protocolo where tenant_id=@TenantId and coalesce(is_deleted,false)=false order by created_at desc limit 5;", new { TenantId = tenantId }, cancellationToken).ConfigureAwait(false);
            var tarefasCriticas = await QueryOrEmptyAsync<DashboardListItemViewModel>(connection, @"select titulo as Titulo, coalesce(dados_json->>'prazo','Tarefa operacional') as Descricao, status as Status, '/Tarefas' as Url, created_at as Data from sigov.tarefa where tenant_id=@TenantId and coalesce(is_deleted,false)=false and upper(status) in ('PENDENTE','VENCIDA','ABERTA') order by case when upper(status)='VENCIDA' then 0 else 1 end, created_at limit 5;", new { TenantId = tenantId }, cancellationToken).ConfigureAwait(false);
            var documentosRecentes = await QueryOrEmptyAsync<DashboardListItemViewModel>(connection, @"select titulo as Titulo, classificacao_lgpd as Descricao, status as Status, '/Ged/Detalhe/' || id as Url, created_at as Data from sigov.documento where tenant_id=@TenantId and coalesce(is_deleted,false)=false order by created_at desc limit 5;", new { TenantId = tenantId }, cancellationToken).ConfigureAwait(false);
            var modulos = await ListarModulosAsync(tenantId, cancellationToken).ConfigureAwait(false);
            var hasFallback = new long?[] { protocolosAbertos, protocolosTramitacao, protocolosConcluidos, tarefasPendentes, tarefasVencidas, notificacoesNaoLidas, documentosCriados, outboxProblemas, webhooksFalha }.Any(x => x is null);
            return new DashboardViewModel
            {
                Cards = new[]
                {
                    new DashboardCard("Protocolos abertos", FormatCount(protocolosAbertos), "Protocolos reais aguardando triagem.", protocolosAbertos is null ? "secondary" : "primary"),
                    new DashboardCard("Protocolos em tramitação", FormatCount(protocolosTramitacao), "Fluxos reais em andamento.", protocolosTramitacao is null ? "secondary" : "info"),
                    new DashboardCard("Protocolos concluídos", FormatCount(protocolosConcluidos), "Demandas encerradas ou arquivadas.", protocolosConcluidos is null ? "secondary" : "success"),
                    new DashboardCard("Tarefas pendentes", FormatCount(tarefasPendentes), "Pendências operacionais.", tarefasPendentes is null ? "secondary" : "warning"),
                    new DashboardCard("Tarefas vencidas", FormatCount(tarefasVencidas), "Itens críticos por prazo.", tarefasVencidas is null ? "secondary" : "danger"),
                    new DashboardCard("Notificações não lidas", FormatCount(notificacoesNaoLidas), "Comunicações do usuário/tenant.", notificacoesNaoLidas is null ? "secondary" : "info"),
                    new DashboardCard("Documentos criados", FormatCount(documentosCriados), "Documentos GED persistidos.", documentosCriados is null ? "secondary" : "success"),
                    new DashboardCard("Outbox pendente/falho", FormatCount(outboxProblemas), "Eventos aguardando entrega ou correção.", outboxProblemas is null ? "secondary" : "warning"),
                    new DashboardCard("Webhooks com falha", FormatCount(webhooksFalha), "Entregas recentes sem sucesso.", webhooksFalha is null ? "secondary" : "danger")
                },
                Ambiente = CriarAmbiente(!hasFallback),
                Modulos = modulos,
                ProtocolosPorStatus = protocolosPorStatus,
                UltimosProtocolos = ultimosProtocolos,
                TarefasCriticas = tarefasCriticas,
                DocumentosRecentes = documentosRecentes,
                AlertaOperacional = (outboxProblemas.GetValueOrDefault() + webhooksFalha.GetValueOrDefault()) > 0 ? "Há eventos outbox ou entregas webhook com pendência/falha. Acompanhe /Operacao/Outbox e Integrações." : string.Empty,
                MensagemFallback = hasFallback ? "Dashboard em modo schema-safe: uma ou mais tabelas operacionais ainda não estão disponíveis; nenhum dado foi simulado como real." : string.Empty
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falha ao montar dashboard operacional.");
            return new DashboardViewModel { Cards = FallbackCards(), Ambiente = CriarAmbiente(false), Modulos = DefaultModules, MensagemFallback = "Não foi possível consultar dados reais agora. Exibindo fallback honesto sem classificar dados como funcionais." };
        }
    }

    public async Task<IReadOnlyCollection<HealthItemViewModel>> VerificarAmbienteAsync(CancellationToken cancellationToken)
    {
        var items = new List<HealthItemViewModel>
        {
            new("Web", "Online", "Aplicação MVC/Razor respondeu à requisição atual.", true)
        };

        try
        {
            using var connection = _connectionFactory.CreateConnection();
            var one = await connection.ExecuteScalarAsync<int>(new CommandDefinition("select 1;", cancellationToken: cancellationToken)).ConfigureAwait(false);
            items.Add(new("PostgreSQL", one == 1 ? "Online" : "Atenção", one == 1 ? "Conexão aberta e select 1 executado." : "Conexão retornou resposta inesperada.", one == 1));

            var migrationsTable = await _schemaInspector.TableExistsAsync("public", "__EFMigrationsHistory", cancellationToken).ConfigureAwait(false)
                || await _schemaInspector.TableExistsAsync("sigov", "__EFMigrationsHistory", cancellationToken).ConfigureAwait(false);
            if (migrationsTable)
            {
                var schema = await _schemaInspector.TableExistsAsync("sigov", "__EFMigrationsHistory", cancellationToken).ConfigureAwait(false) ? "sigov" : "public";
                var last = await connection.ExecuteScalarAsync<string?>(new CommandDefinition($"select \"MigrationId\" from {schema}.\"__EFMigrationsHistory\" order by \"MigrationId\" desc limit 1;", cancellationToken: cancellationToken)).ConfigureAwait(false);
                items.Add(new("Migrations", "Online", string.IsNullOrWhiteSpace(last) ? "Tabela de migrations existe, sem registros." : $"Última migration aplicada: {last}.", true));
            }
            else
            {
                items.Add(new("Migrations", "Atenção", "Tabela de controle de migrations não encontrada.", false));
            }

            var workerTable = await _schemaInspector.TableExistsAsync("sigov", "worker_heartbeat", cancellationToken).ConfigureAwait(false);
            items.Add(workerTable ? new("Worker", "Atenção", "Tabela de heartbeat encontrada; validar timestamp em relatório operacional.", false) : new("Worker", "Não monitorado", "Nenhuma tabela de heartbeat foi encontrada; status online não é inferido.", false));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Falha no probe real de PostgreSQL/migrations/worker.");
            items.Add(new("PostgreSQL", "Offline", "Não foi possível abrir conexão e executar select 1.", false));
            items.Add(new("Migrations", "Atenção", "Não verificadas porque o banco está indisponível.", false));
            items.Add(new("Worker", "Não monitorado", "Sem prova real de heartbeat.", false));
        }

        try
        {
            var storage = Environment.GetEnvironmentVariable("SIGOV_STORAGE_PATH") ?? Path.Combine(AppContext.BaseDirectory, "storage");
            Directory.CreateDirectory(storage);
            var probe = Path.Combine(storage, $".sigov-health-{Guid.NewGuid():N}.tmp");
            await File.WriteAllTextAsync(probe, "ok", cancellationToken).ConfigureAwait(false);
            File.Delete(probe);
            items.Add(new("Storage", "Online", $"Diretório gravável: {storage}.", true));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Falha no probe real de storage.");
            items.Add(new("Storage", "Offline", "Não foi possível criar e remover arquivo temporário.", false));
        }

        items.Add(new("API", "Não monitorado", "Validar /api/health/live via smoke test HTTP externo; a Web não declara online sem chamada real.", false));
        return items;
    }

    public IReadOnlyCollection<HealthItemViewModel> CriarAmbiente(bool databaseOnline) => new[]
    {
        new HealthItemViewModel("Web", "Atenção", "Fallback de health exibido; validar a requisição atual e logs.", false),
        new HealthItemViewModel("API", "Não monitorado", "Sem probe HTTP real neste fallback.", false),
        new HealthItemViewModel("Worker", "Não monitorado", "Sem heartbeat real neste fallback.", false),
        new HealthItemViewModel("PostgreSQL", databaseOnline ? "Online" : "Offline", databaseOnline ? "Conexão local OK." : "Banco indisponível no momento.", databaseOnline),
        new HealthItemViewModel("Migrations", databaseOnline ? "Atenção" : "Atenção", "Validar tabela de controle antes de declarar online.", false),
        new HealthItemViewModel("Storage local", "Não monitorado", "Sem probe de escrita neste fallback.", false)
    };

    private static IReadOnlyCollection<DashboardCard> FallbackCards() => new[]
    {
        new DashboardCard("Clientes/Tenants ativos", "--", "Fallback sem conexão.", "secondary"),
        new DashboardCard("Usuários ativos", "--", "Fallback sem conexão.", "secondary"),
        new DashboardCard("Módulos disponíveis", DefaultModules.Count.ToString(), "Catálogo local.", "info"),
        new DashboardCard("Status da API", "Verificar", "Use /Operacao/Health.", "warning"),
        new DashboardCard("Status do banco", "Verificar", "Use scripts/check-local.ps1.", "warning"),
        new DashboardCard("Migrations", "Verificar", "Veja logs db-migrations.", "warning")
    };

    private async Task<long?> CountOrNullAsync(System.Data.IDbConnection connection, string sql, CancellationToken cancellationToken, object? parameters = null)
    {
        try
        {
            return await connection.ExecuteScalarAsync<long>(new CommandDefinition(sql, parameters, cancellationToken: cancellationToken)).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Consulta de indicador indisponível para o dashboard. Sql={Sql}", sql);
            return null;
        }
    }

    private static async Task AuditarSaasAsync(System.Data.IDbConnection connection, string acao, string entidade, long id, object payload, CancellationToken cancellationToken)
    {
        try
        {
            var json = System.Text.Json.JsonSerializer.Serialize(payload);
            await connection.ExecuteAsync(new CommandDefinition("insert into sigov.auditoria_evento (acao, entidade, entidade_id, depois, created_at) values (@Acao, @Entidade, @Id, @Depois::jsonb, now());", new { Acao = acao, Entidade = entidade, Id = id.ToString(System.Globalization.CultureInfo.InvariantCulture), Depois = json }, cancellationToken: cancellationToken)).ConfigureAwait(false);
        }
        catch
        {
        }
    }

    private static void AddSet(List<string> sets, IReadOnlySet<string> columns, string column, string parameter)
    {
        if (columns.Contains(column)) sets.Add($"{column}={parameter}");
    }

    private static void AddInsert(List<string> cols, List<string> vals, IReadOnlySet<string> columns, string column, string value)
    {
        if (columns.Contains(column)) { cols.Add(column); vals.Add(value); }
    }

    private static string Slugify(string value)
    {
        var normalized = value.Normalize(System.Text.NormalizationForm.FormD);
        var chars = normalized.Where(c => System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c) != System.Globalization.UnicodeCategory.NonSpacingMark)
            .Select(c => char.IsLetterOrDigit(c) ? char.ToLowerInvariant(c) : '-')
            .ToArray();
        return string.Join('-', new string(chars).Split('-', StringSplitOptions.RemoveEmptyEntries)).Trim('-');
    }

    private async Task<IReadOnlyCollection<T>> QueryOrEmptyAsync<T>(System.Data.IDbConnection connection, string sql, object parameters, CancellationToken cancellationToken)
    {
        try
        {
            return (await connection.QueryAsync<T>(new CommandDefinition(sql, parameters, cancellationToken: cancellationToken)).ConfigureAwait(false)).ToArray();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Consulta parcial do dashboard não disponível.");
            return Array.Empty<T>();
        }
    }

    private static string FormatCount(long? value) => value.HasValue ? value.Value.ToString() : "--";
    private static SigovFeatureStatus NormalizeStatus(string status)
    {
        if (string.IsNullOrWhiteSpace(status)) return SigovFeatureStatus.Indisponivel;
        var normalized = status.Trim().Replace("_", " ").Replace("-", " ").ToUpperInvariant();
        if (normalized is "ATIVO" or "HABILITADO" or "FUNCIONAL" or "DISPONIVEL" or "DISPONÍVEL") return SigovFeatureStatus.Funcional;
        if (normalized.Contains("PARCIAL", StringComparison.Ordinal) || normalized is "SUSPENSO") return SigovFeatureStatus.Parcial;
        if (normalized.Contains("DEMON", StringComparison.Ordinal)) return SigovFeatureStatus.Demonstrativo;
        if (normalized.Contains("IMPLANT", StringComparison.Ordinal) || normalized is "ROADMAP") return SigovFeatureStatus.EmImplantacao;
        return SigovFeatureStatus.Indisponivel;
    }
    private static string MaskDocument(string value) => LgpdMaskingHelper.MaskDocument(value);
    private static string MaskEmail(string value) => LgpdMaskingHelper.MaskEmail(value);
    private static string MaskPhone(string value) => LgpdMaskingHelper.MaskPhone(value);

    private sealed record TenantRow(long Id, string Nome, string Codigo, string Documento, string Email, string Telefone, string Plano, bool Ativo);
    private static ParametroSaasItemViewModel ToViewModel(ParametroRow row) => new(row.Id, row.Chave, row.Valor, row.Tipo, row.Descricao, row.Sensivel);
    private static string NormalizeParameterType(string? tipo) => (tipo ?? "string").Trim().ToLowerInvariant() switch { "boolean" or "bool" or "booleano" => "bool", "number" or "decimal" or "numero" => "decimal", "integer" or "int" or "inteiro" => "int", "password" or "senha" or "segredo" => "string", "json" => "json", "date" or "data" => "date", _ => "string" };
    private static (bool Ok, string Mensagem) ValidateParameterValue(string tipo, string? valor)
    {
        var value = valor ?? string.Empty;
        if (tipo == "bool" && !bool.TryParse(value, out _)) return (false, "Valor booleano deve ser true ou false.");
        if (tipo == "int" && !int.TryParse(value, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out _)) return (false, "Valor inteiro deve usar formato invariável, exemplo 123.");
        if (tipo == "decimal" && !decimal.TryParse(value, System.Globalization.NumberStyles.Number, System.Globalization.CultureInfo.InvariantCulture, out _)) return (false, "Valor decimal deve usar formato invariável, exemplo 123.45.");
        if (tipo == "date" && !DateTime.TryParse(value, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out _)) return (false, "Data inválida; use formato ISO quando possível, exemplo 2026-07-01.");
        if (tipo == "json") { try { System.Text.Json.JsonDocument.Parse(string.IsNullOrWhiteSpace(value) ? "{}" : value); } catch { return (false, "JSON inválido."); } }
        return (true, string.Empty);
    }
    private sealed record ModuleStatusRow(string Codigo, string Status);
    private sealed record ParametroRow(long Id, string Chave, string Valor, string Tipo, string Descricao, bool Sensivel);

    public async Task<SaasPlanosViewModel> ListarPlanosAsync(CancellationToken cancellationToken)
    {
        try
        {
            var columns = await _schemaInspector.GetColumnsAsync("sigov", "plano_saas", cancellationToken).ConfigureAwait(false);
            if (!columns.Contains("id") || !columns.Contains("nome"))
            {
                return new SaasPlanosViewModel { Planos = DefaultPlans(), PodePersistir = false, MensagemFallback = "Tabela sigov.plano_saas indisponível; catálogo exibido é demonstrativo e nenhum salvamento é simulado." };
            }
            using var connection = _connectionFactory.CreateConnection();
            var codigo = columns.Contains("codigo") ? "coalesce(codigo, id::text)" : "id::text";
            var descricao = columns.Contains("descricao") ? "coalesce(descricao,'')" : "''";
            var mensal = columns.Contains("valor_mensal") ? "coalesce(valor_mensal,0)" : "0";
            var anual = columns.Contains("valor_anual") ? "coalesce(valor_anual,0)" : "0";
            var usuarios = columns.Contains("limite_usuarios") ? "coalesce(limite_usuarios,0)" : "0";
            var storage = columns.Contains("limite_storage_gb") ? "coalesce(limite_storage_gb,0)" : "0";
            var tenants = columns.Contains("limite_tenants") ? "coalesce(limite_tenants,0)" : "0";
            var suporte = columns.Contains("suporte_incluso") ? "coalesce(suporte_incluso,'')" : "''";
            var modulos = columns.Contains("modulos_inclusos") ? "coalesce(modulos_inclusos::text,'')" : "''";
            var ativo = columns.Contains("ativo") ? "coalesce(ativo,true)" : "true";
            var recomendado = columns.Contains("recomendado") ? "coalesce(recomendado,false)" : "false";
            var ordem = columns.Contains("ordem") ? "coalesce(ordem,0)" : "0";
            var rows = await connection.QueryAsync<SaasPlanoRow>(new CommandDefinition($@"select id, {codigo} as Codigo, nome, {descricao} as Descricao, {mensal} as ValorMensal, {anual} as ValorAnual, {usuarios} as LimiteUsuarios, {storage} as LimiteStorageGb, {tenants} as LimiteTenants, {suporte} as Suporte, {modulos} as ModulosInclusos, {ativo} as Ativo, {recomendado} as Recomendado, {ordem} as Ordem from sigov.plano_saas order by {ordem}, nome limit 100;", cancellationToken: cancellationToken)).ConfigureAwait(false);
            return new SaasPlanosViewModel { Planos = rows.Select(x => new SaasPlanoViewModel(x.Id, x.Codigo, x.Nome, x.Descricao, x.ValorMensal, x.ValorAnual, x.LimiteUsuarios, x.LimiteStorageGb, x.LimiteTenants, x.Suporte, x.ModulosInclusos, x.Ativo, x.Recomendado, x.Ordem, true)).ToArray(), PodePersistir = true };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falha ao listar planos SaaS.");
            return new SaasPlanosViewModel { Planos = DefaultPlans(), PodePersistir = false, MensagemFallback = "Não foi possível consultar planos reais; catálogo demonstrativo seguro exibido." };
        }
    }

    public async Task<SaasAssinaturasViewModel> ListarAssinaturasAsync(CancellationToken cancellationToken)
    {
        try
        {
            if (!await _schemaInspector.TableExistsAsync("sigov", "assinatura_saas", cancellationToken).ConfigureAwait(false))
                return new SaasAssinaturasViewModel { PodePersistir = false, MensagemFallback = "Assinaturas em implantação: tabela sigov.assinatura_saas não encontrada; nenhuma contratação é simulada." };
            using var connection = _connectionFactory.CreateConnection();
            var rows = await connection.QueryAsync<SaasAssinaturaViewModel>(new CommandDefinition(@"select a.id, coalesce(a.tenant_id,0) as TenantId, coalesce(t.nome,'Tenant não informado') as Tenant, coalesce(a.plano_id,0) as PlanoId, coalesce(p.nome,'Plano não informado') as Plano, coalesce(a.status,'Trial') as Status, a.data_inicio as Inicio, a.data_fim as Fim, coalesce(a.valor,0) as Valor, coalesce(a.ciclo_cobranca,'mensal') as Ciclo, coalesce(a.limite_usuarios,0) as LimiteUsuarios, coalesce(a.limite_storage_gb,0) as LimiteStorageGb, coalesce(a.observacoes,'') as Observacoes, coalesce(a.modulos_incluidos::text,'') as ModulosIncluidos, true as Persistida from sigov.assinatura_saas a left join sigov.tenant t on t.id=a.tenant_id left join sigov.plano_saas p on p.id=a.plano_id order by a.id desc limit 100;", cancellationToken: cancellationToken)).ConfigureAwait(false);
            return new SaasAssinaturasViewModel { Assinaturas = rows.ToArray(), PodePersistir = true };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falha ao listar assinaturas SaaS.");
            return new SaasAssinaturasViewModel { PodePersistir = false, MensagemFallback = "Assinaturas indisponíveis no schema atual; nenhuma informação foi simulada." };
        }
    }

    public async Task<SaasNotificationsViewModel> ListarNotificacoesAsync(string? status, CancellationToken cancellationToken)
    {
        try
        {
            if (await _schemaInspector.TableExistsAsync("sigov", "notificacao", cancellationToken).ConfigureAwait(false))
            {
                using var connection = _connectionFactory.CreateConnection();
                var rows = await connection.QueryAsync<SaasNotificationViewModel>(new CommandDefinition(@"select id, coalesce(tipo,'geral') as Tipo, coalesce(titulo,'Notificação') as Titulo, coalesce(descricao,'') as Descricao, coalesce(status,'nao_lida') as Status, created_at as Data, true as Persistida from sigov.notificacao where (@Status is null or status=@Status) order by created_at desc limit 100;", new { Status = string.IsNullOrWhiteSpace(status) ? null : status }, cancellationToken: cancellationToken)).ConfigureAwait(false);
                return new SaasNotificationsViewModel { Notificacoes = rows.ToArray(), PodeMarcarLida = true, StatusFiltro = status ?? string.Empty };
            }
            var derived = new[]
            {
                new SaasNotificationViewModel(0, "implantacao", "Implantação pendente", "Revise onboarding, parâmetros e módulos antes do go-live.", "recomendacao", DateTimeOffset.UtcNow, false),
                new SaasNotificationViewModel(0, "health", "Health deve ser validado", "Sem tabela de notificações; alerta derivado do checklist operacional.", "recomendacao", DateTimeOffset.UtcNow, false),
                new SaasNotificationViewModel(0, "lgpd", "Acesso auditado", "Dados pessoais são mascarados e eventos críticos devem ser acompanhados.", "recomendacao", DateTimeOffset.UtcNow, false)
            };
            return new SaasNotificationsViewModel { Notificacoes = derived, PodeMarcarLida = false, StatusFiltro = status ?? string.Empty, MensagemFallback = "Tabela de notificações indisponível; exibindo recomendações derivadas, sem marcar como lida." };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falha ao listar notificações.");
            return new SaasNotificationsViewModel { MensagemFallback = "Não foi possível carregar notificações agora." };
        }
    }

    public async Task<GlobalSearchViewModel> BuscarAsync(string? q, CancellationToken cancellationToken)
    {
        var query = (q ?? string.Empty).Trim();
        if (query.Length < 2) return new GlobalSearchViewModel { Query = query, MensagemFallback = "Informe ao menos 2 caracteres para buscar." };
        var results = new List<GlobalSearchResultViewModel>();
        var ignored = new List<string>();
        var tenantId = 1L;
        async Task SearchSql(string area, string table, string sql, object parameters)
        {
            try
            {
                if (!await _schemaInspector.TableExistsAsync("sigov", table, cancellationToken).ConfigureAwait(false)) { ignored.Add(area); return; }
                using var connection = _connectionFactory.CreateConnection();
                var rows = await connection.QueryAsync<SearchRow>(new CommandDefinition(sql, parameters, cancellationToken: cancellationToken)).ConfigureAwait(false);
                results.AddRange(rows.Select(r => new GlobalSearchResultViewModel(area, MaskSearch(r.Titulo), r.Descricao, r.Url, "Funcional", r.Status, r.Data, r.LgpdMascarado)));
            }
            catch (Exception ex) { _logger.LogWarning(ex, "Busca parcial falhou em {Area}.", area); ignored.Add(area); }
        }
        var p = new { Q = query, TenantId = tenantId };
        await SearchSql("Protocolo", "protocolo", @"select id::text as Id, numero || ' — ' || assunto as Titulo, 'Interessado/documento sempre mascarado por LGPD' as Descricao, status as Status, '/Protocolo/Detalhe/' || id as Url, created_at as Data, true as LgpdMascarado from sigov.protocolo where tenant_id=@TenantId and coalesce(is_deleted,false)=false and (numero ilike '%'||@Q||'%' or assunto ilike '%'||@Q||'%' or status ilike '%'||@Q||'%' or coalesce(dados_json->>'interessado','') ilike '%'||@Q||'%') order by created_at desc limit 8;", p).ConfigureAwait(false);
        await SearchSql("Movimento", "protocolo_movimento", @"select id::text as Id, 'Movimento de protocolo' as Titulo, left(coalesce(observacao,''),180) as Descricao, status as Status, '/Protocolo' as Url, created_at as Data, false as LgpdMascarado from sigov.protocolo_movimento where tenant_id=@TenantId and coalesce(is_deleted,false)=false and (coalesce(observacao,'') ilike '%'||@Q||'%' or status ilike '%'||@Q||'%') order by created_at desc limit 5;", p).ConfigureAwait(false);
        await SearchSql("Documento", "documento", @"select id::text as Id, titulo as Titulo, 'Documento GED sem storage_path exposto' as Descricao, status as Status, '/Ged/Detalhe/' || id as Url, created_at as Data, false as LgpdMascarado from sigov.documento where tenant_id=@TenantId and coalesce(is_deleted,false)=false and (titulo ilike '%'||@Q||'%' or codigo ilike '%'||@Q||'%' or status ilike '%'||@Q||'%') order by created_at desc limit 8;", p).ConfigureAwait(false);
        await SearchSql("Versão GED", "documento_versao", @"select id::text as Id, 'Versão GED #' || versao as Titulo, hash_sha256 as Descricao, status as Status, '/Ged' as Url, created_at as Data, false as LgpdMascarado from sigov.documento_versao where tenant_id=@TenantId and coalesce(is_deleted,false)=false and (hash_sha256 ilike '%'||@Q||'%' or status ilike '%'||@Q||'%') order by created_at desc limit 5;", p).ConfigureAwait(false);
        await SearchSql("Workflow", "workflow_instancia", @"select id::text as Id, 'Workflow #' || id as Titulo, coalesce(dados_json::text,'') as Descricao, status as Status, '/Workflow' as Url, created_at as Data, false as LgpdMascarado from sigov.workflow_instancia where tenant_id=@TenantId and coalesce(is_deleted,false)=false and (status ilike '%'||@Q||'%' or coalesce(dados_json::text,'') ilike '%'||@Q||'%') order by created_at desc limit 5;", p).ConfigureAwait(false);
        await SearchSql("Tarefa", "tarefa", @"select id::text as Id, titulo as Titulo, coalesce(dados_json::text,'') as Descricao, status as Status, '/Tarefas' as Url, created_at as Data, false as LgpdMascarado from sigov.tarefa where tenant_id=@TenantId and coalesce(is_deleted,false)=false and (titulo ilike '%'||@Q||'%' or status ilike '%'||@Q||'%') order by created_at desc limit 8;", p).ConfigureAwait(false);
        await SearchSql("Notificação", "notificacao", @"select id::text as Id, titulo as Titulo, left(coalesce(mensagem,''),180) as Descricao, status as Status, '/Notificacoes' as Url, created_at as Data, false as LgpdMascarado from sigov.notificacao where tenant_id=@TenantId and coalesce(is_deleted,false)=false and (titulo ilike '%'||@Q||'%' or coalesce(mensagem,'') ilike '%'||@Q||'%' or status ilike '%'||@Q||'%') order by created_at desc limit 8;", p).ConfigureAwait(false);
        return new GlobalSearchViewModel { Query = query, Resultados = results, AreasIgnoradas = ignored.Distinct().ToArray(), MensagemFallback = ignored.Count > 0 ? "Algumas áreas foram ignoradas porque o schema não está disponível; resultados reais encontrados respeitam tenant e mascaramento LGPD." : string.Empty };
    }

    private static IReadOnlyCollection<SaasPlanoViewModel> DefaultPlans() => new[]
    {
        new SaasPlanoViewModel(0, "starter", "Starter", "Catálogo demonstrativo para operação inicial.", 990, 9900, 15, 10, 1, "Comercial", "Dashboard, Usuários, Ajuda", true, false, 1, false),
        new SaasPlanoViewModel(0, "gov-plus", "Gov Plus", "Catálogo demonstrativo recomendado para gestão pública integrada.", 4990, 49900, 150, 150, 5, "Prioritário", "Tributário, Protocolo, GED, LGPD", true, true, 2, false),
        new SaasPlanoViewModel(0, "enterprise", "Enterprise", "Catálogo demonstrativo para multi-entidade e white label.", 12990, 129900, 0, 1024, 0, "SLA premium", "Todos os módulos, integrações, white label", true, false, 3, false)
    };
    private static string MaskSearch(string value) => string.IsNullOrWhiteSpace(value) ? string.Empty : (value.Contains('@') ? MaskEmail(value) : value);
    private sealed record SaasPlanoRow(long Id, string Codigo, string Nome, string Descricao, decimal ValorMensal, decimal ValorAnual, int LimiteUsuarios, int LimiteStorageGb, int LimiteTenants, string Suporte, string ModulosInclusos, bool Ativo, bool Recomendado, int Ordem);
    private sealed record SearchRow(string Id, string Titulo, string Descricao, string Status, string Url, DateTimeOffset? Data, bool LgpdMascarado);

}
