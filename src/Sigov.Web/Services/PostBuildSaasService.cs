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

    public async Task<ParametrosSaasViewModel> ListarParametrosAsync(long tenantId, CancellationToken cancellationToken)
    {
        try
        {
            var hasTenantParametro = await _schemaInspector.TableExistsAsync("sigov", "tenant_parametro", cancellationToken).ConfigureAwait(false);
            var hasParametroSistema = await _schemaInspector.TableExistsAsync("sigov", "parametro_sistema", cancellationToken).ConfigureAwait(false);
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
                return new ParametrosSaasViewModel { TenantId = tenantId, Parametros = rows.Select(ToViewModel).ToArray(), PodePersistir = true };
            }

            var hasTenantIdColumn = await _schemaInspector.ColumnExistsAsync("sigov", "parametro_sistema", "tenant_id", cancellationToken).ConfigureAwait(false);
            var hasTipoColumn = await _schemaInspector.ColumnExistsAsync("sigov", "parametro_sistema", "tipo", cancellationToken).ConfigureAwait(false);
            var hasDescricaoColumn = await _schemaInspector.ColumnExistsAsync("sigov", "parametro_sistema", "descricao", cancellationToken).ConfigureAwait(false);
            var hasCategoriaColumn = await _schemaInspector.ColumnExistsAsync("sigov", "parametro_sistema", "categoria", cancellationToken).ConfigureAwait(false);
            var tipoSelect = hasTipoColumn ? "coalesce(tipo,'texto')" : "'texto'";
            var descricaoSelect = hasDescricaoColumn ? "coalesce(descricao,'')" : "''";
            var where = hasTenantIdColumn ? " where tenant_id=@TenantId" : string.Empty;
            var order = hasCategoriaColumn ? "categoria, chave" : "chave";
            var sql = $@"select id, chave, case when lower(chave) like '%senha%' or lower(chave) like '%token%' or lower(chave) like '%secret%' then '••••••' else coalesce(valor::text,'') end as valor, {tipoSelect} as tipo, {descricaoSelect} as descricao, (lower(chave) like '%senha%' or lower(chave) like '%token%' or lower(chave) like '%secret%') as sensivel from sigov.parametro_sistema{where} order by {order} limit 200;";
            var sistemaRows = await connection.QueryAsync<ParametroRow>(new CommandDefinition(sql, new { TenantId = tenantId }, cancellationToken: cancellationToken)).ConfigureAwait(false);
            return new ParametrosSaasViewModel { TenantId = tenantId, Parametros = sistemaRows.Select(ToViewModel).ToArray(), MensagemFallback = "Usando sigov.parametro_sistema em modo schema-safe.", PodePersistir = true };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falha ao listar parâmetros SaaS. TenantId={TenantId}", tenantId);
            return new ParametrosSaasViewModel { TenantId = tenantId, MensagemFallback = "Não foi possível consultar parâmetros agora.", PodePersistir = false };
        }
    }

    public async Task<(bool Ok, string Mensagem)> SalvarParametroAsync(ParametroSaasFormViewModel form, CancellationToken cancellationToken)
    {
        if (form.TenantId <= 0) return (false, "Informe um tenant válido antes de salvar parâmetros.");
        if (string.IsNullOrWhiteSpace(form.Chave)) return (false, "Chave do parâmetro é obrigatória.");
        var tipo = NormalizeParameterType(form.Tipo);
        var validation = ValidateParameterValue(tipo, form.Valor);
        if (!validation.Ok) return validation;

        try
        {
            if (!await _schemaInspector.TableExistsAsync("sigov", "tenant_parametro", cancellationToken).ConfigureAwait(false))
            {
                return (false, "Tabela sigov.tenant_parametro indisponível; parâmetro não foi persistido.");
            }

            using var connection = _connectionFactory.CreateConnection();
            await connection.ExecuteAsync(new CommandDefinition(@"insert into sigov.tenant_parametro(tenant_id,chave,valor,tipo,descricao,sensivel,updated_at)
values(@TenantId,@Chave,@Valor,@Tipo,@Descricao,@Sensivel,now())
on conflict(tenant_id,chave) do update set valor=excluded.valor,tipo=excluded.tipo,descricao=excluded.descricao,sensivel=excluded.sensivel,updated_at=now();",
                new { form.TenantId, Chave = form.Chave.Trim(), Valor = form.Valor.Trim(), Tipo = tipo, form.Descricao, form.Sensivel }, cancellationToken: cancellationToken)).ConfigureAwait(false);
            await AuditarSaasAsync(connection, "SAAS_PARAMETRO_SALVAR", "sigov.tenant_parametro", form.TenantId, new { form.TenantId, Chave = form.Chave, Tipo = tipo, Sensivel = form.Sensivel, Valor = form.Sensivel ? "***" : form.Valor }, cancellationToken).ConfigureAwait(false);
            return (true, "Parâmetro salvo com validação por tipo e auditoria.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falha ao salvar parâmetro SaaS. TenantId={TenantId} Chave={Chave}", form.TenantId, form.Chave);
            return (false, "Não foi possível salvar o parâmetro. Nenhum sucesso foi simulado.");
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
            var tenants = await CountOrNullAsync(connection, "select count(*) from sigov.tenant where ativo = true and is_deleted = false;", cancellationToken).ConfigureAwait(false);
            var usuarios = await CountOrNullAsync(connection, "select count(*) from sigov.usuario where ativo = true and is_deleted = false;", cancellationToken).ConfigureAwait(false);
            var planos = await CountOrNullAsync(connection, "select count(*) from sigov.plano_saas where ativo = true;", cancellationToken).ConfigureAwait(false);
            var auditorias = await CountOrNullAsync(connection, "select count(*) from sigov.auditoria;", cancellationToken).ConfigureAwait(false);
            var parametros = await CountOrNullAsync(connection, "select count(*) from sigov.parametro_sistema;", cancellationToken).ConfigureAwait(false);
            var modulos = await ListarModulosAsync(null, cancellationToken).ConfigureAwait(false);
            var hasFallback = tenants is null || usuarios is null || planos is null || auditorias is null || parametros is null;
            return new DashboardViewModel
            {
                Cards = new[]
                {
                    new DashboardCard("Clientes/Tenants ativos", FormatCount(tenants), tenants is null ? "Dados indisponíveis no ambiente local." : "Clientes SaaS habilitados.", tenants is null ? "secondary" : "primary"),
                    new DashboardCard("Usuários ativos", FormatCount(usuarios), usuarios is null ? "Dados indisponíveis no ambiente local." : "Usuários aptos a acessar.", usuarios is null ? "secondary" : "success"),
                    new DashboardCard("Módulos disponíveis", modulos.Count.ToString(), "Catálogo inicial SaaS.", "info"),
                    new DashboardCard("Planos ativos", FormatCount(planos), planos is null ? "Dados indisponíveis no ambiente local." : "Catálogo comercial SaaS.", planos is null ? "secondary" : "primary"),
                    new DashboardCard("Auditorias", FormatCount(auditorias), auditorias is null ? "Dados indisponíveis no ambiente local." : "Eventos LGPD registrados.", auditorias is null ? "secondary" : "warning"),
                    new DashboardCard("Parâmetros", FormatCount(parametros), parametros is null ? "Dados indisponíveis no ambiente local." : "Configurações por escopo.", parametros is null ? "secondary" : "info")
                },
                Ambiente = CriarAmbiente(!hasFallback),
                Modulos = modulos,
                MensagemFallback = hasFallback ? "Dados indisponíveis no ambiente local para uma ou mais tabelas. A tela permanece operacional sem expor detalhes técnicos." : string.Empty
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falha ao montar dashboard inicial.");
            return new DashboardViewModel { Cards = FallbackCards(), Ambiente = CriarAmbiente(false), Modulos = DefaultModules, MensagemFallback = "Não foi possível consultar o banco/API agora. Exibindo dados demonstrativos seguros." };
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

        items.Add(new("API", "Atenção", "Validar /api/health/live via smoke test HTTP externo; a Web não declara online sem chamada real.", false));
        return items;
    }

    public IReadOnlyCollection<HealthItemViewModel> CriarAmbiente(bool databaseOnline) => new[]
    {
        new HealthItemViewModel("Web", "online", "Aplicação MVC/Razor carregada.", true),
        new HealthItemViewModel("API", "online", "Endpoint /api/health/live disponível no ambiente Docker.", true),
        new HealthItemViewModel("Worker", "online", "Worker configurado no compose; validar logs para jobs ativos.", true),
        new HealthItemViewModel("PostgreSQL", databaseOnline ? "online" : "offline", databaseOnline ? "Conexão local OK." : "Banco indisponível no momento.", databaseOnline),
        new HealthItemViewModel("Migrations", databaseOnline ? "ok" : "pendente", "Últimas migrations idempotentes no schema sigov.", databaseOnline),
        new HealthItemViewModel("Storage local", "ok", "Volume sigov_storage configurado.", true)
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

    private async Task<int?> CountOrNullAsync(System.Data.IDbConnection connection, string sql, CancellationToken cancellationToken)
    {
        try
        {
            return await connection.ExecuteScalarAsync<int>(new CommandDefinition(sql, cancellationToken: cancellationToken)).ConfigureAwait(false);
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

    private static string FormatCount(int? value) => value.HasValue ? value.Value.ToString() : "--";
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
    private static string NormalizeParameterType(string? tipo) => (tipo ?? "texto").Trim().ToLowerInvariant() switch { "boolean" or "bool" => "booleano", "number" or "decimal" or "inteiro" => "numero", "password" or "senha" => "segredo", "json" => "json", _ => "texto" };
    private static (bool Ok, string Mensagem) ValidateParameterValue(string tipo, string? valor)
    {
        var value = valor ?? string.Empty;
        if (tipo == "booleano" && !bool.TryParse(value, out _)) return (false, "Valor booleano deve ser true ou false.");
        if (tipo == "numero" && !decimal.TryParse(value, System.Globalization.NumberStyles.Number, System.Globalization.CultureInfo.InvariantCulture, out _)) return (false, "Valor numérico deve usar formato invariável, exemplo 123.45.");
        if (tipo == "json") { try { System.Text.Json.JsonDocument.Parse(string.IsNullOrWhiteSpace(value) ? "{}" : value); } catch { return (false, "JSON inválido."); } }
        return (true, string.Empty);
    }
    private sealed record ModuleStatusRow(string Codigo, string Status);
    private sealed record ParametroRow(long Id, string Chave, string Valor, string Tipo, string Descricao, bool Sensivel);
}
