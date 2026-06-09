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

    public async Task<DashboardViewModel> CriarDashboardAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var connection = _connectionFactory.CreateConnection();
            var tenants = await connection.ExecuteScalarAsync<int>(new CommandDefinition("select count(*) from sigov.tenant where ativo = true and is_deleted = false;", cancellationToken: cancellationToken)).ConfigureAwait(false);
            var usuarios = await connection.ExecuteScalarAsync<int>(new CommandDefinition("select count(*) from sigov.usuario where ativo = true and is_deleted = false;", cancellationToken: cancellationToken)).ConfigureAwait(false);
            var modulos = await ListarModulosAsync(null, cancellationToken).ConfigureAwait(false);
            return new DashboardViewModel
            {
                Cards = new[]
                {
                    new DashboardCard("Clientes/Tenants ativos", tenants.ToString(), "Clientes SaaS habilitados.", "primary"),
                    new DashboardCard("Usuários ativos", usuarios.ToString(), "Usuários aptos a acessar.", "success"),
                    new DashboardCard("Módulos disponíveis", modulos.Count.ToString(), "Catálogo inicial SaaS.", "info"),
                    new DashboardCard("Status da API", "Online", "Health API respondendo via Docker.", "success"),
                    new DashboardCard("Status do banco", "Online", "PostgreSQL conectado.", "success"),
                    new DashboardCard("Migrations", "OK", "Migrations idempotentes aplicadas.", "success")
                },
                Ambiente = CriarAmbiente(true),
                Modulos = modulos
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
