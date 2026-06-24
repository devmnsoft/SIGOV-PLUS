using Dapper;
using Sigov.Application.Abstractions;
using Sigov.Infrastructure.Persistence.Dapper;
using Sigov.Web.Models.PostBuild;

namespace Sigov.Web.Services;

public sealed class PostBuildSaasService
{
    private static readonly IReadOnlyCollection<ModuleViewModel> DefaultModules = new[]
    {
        new ModuleViewModel("tributario", "Tributário", "disponível", "Receitas, dívida ativa e arrecadação."),
        new ModuleViewModel("rh", "RH", "disponível", "Pessoas, vínculos e folha."),
        new ModuleViewModel("juridico", "Jurídico", "em implantação", "Processos e pareceres jurídicos."),
        new ModuleViewModel("contratos", "Contratos", "em implantação", "Gestão contratual."),
        new ModuleViewModel("ged", "GED", "em implantação", "Gestão eletrônica de documentos."),
        new ModuleViewModel("protocolo", "Protocolo", "em implantação", "Atendimento e processos digitais."),
        new ModuleViewModel("saude", "Saúde", "disponível", "Atenção básica e vigilância."),
        new ModuleViewModel("educacao", "Educação", "disponível", "Escolas, matrículas e frequência."),
        new ModuleViewModel("agro", "Agro", "disponível", "Produtores, propriedades e programas rurais."),
        new ModuleViewModel("saneamento", "Saneamento", "disponível", "Serviços e indicadores de saneamento."),
        new ModuleViewModel("social", "Assistência Social", "disponível", "Cadastros e atendimentos sociais."),
        new ModuleViewModel("integracoes", "Integrações", "em implantação", "APIs, webhooks e conectores.")
    };

    private readonly NpgsqlConnectionFactory _connectionFactory;
    private readonly ILogger<PostBuildSaasService> _logger;

    public PostBuildSaasService(NpgsqlConnectionFactory connectionFactory, ILogger<PostBuildSaasService> logger)
    {
        _connectionFactory = connectionFactory;
        _logger = logger;
    }

    public async Task<IReadOnlyCollection<TenantListItemViewModel>> ListarTenantsAsync(string? busca, CancellationToken cancellationToken)
    {
        const string sql = @"select id,
       nome,
       slug as Codigo,
       coalesce(documento, '') as Documento,
       coalesce(email, '') as Email,
       coalesce(telefone, '') as Telefone,
       coalesce(plano, metadados->>'plano', 'global') as Plano,
       ativo
from sigov.tenant
where is_deleted = false
  and (@Busca is null or nome ilike '%' || @Busca || '%' or slug ilike '%' || @Busca || '%' or coalesce(documento,'') ilike '%' || @Busca || '%')
order by nome
limit 50;";
        try
        {
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

    private static string FormatCount(int? value) => value.HasValue ? value.Value.ToString() : "--";
    private static string NormalizeStatus(string status) => status.Replace('_', ' ').ToLowerInvariant();
    private static string MaskDocument(string value) => string.IsNullOrWhiteSpace(value) || value.Length < 5 ? "***" : $"{value[..2]}***{value[^2..]}";
    private static string MaskEmail(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || !value.Contains('@', StringComparison.Ordinal)) return "***";
        var parts = value.Split('@', 2);
        return $"{parts[0][0]}***@{parts[1]}";
    }
    private static string MaskPhone(string value) => string.IsNullOrWhiteSpace(value) || value.Length < 4 ? "***" : $"***{value[^4..]}";

    private sealed record TenantRow(long Id, string Nome, string Codigo, string Documento, string Email, string Telefone, string Plano, bool Ativo);
    private sealed record ModuleStatusRow(string Codigo, string Status);
}
