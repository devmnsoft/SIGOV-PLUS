using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Sigov.Application.Health;

namespace Sigov.Infrastructure.Health;

public sealed class ProjectStatusProvider
{
    private readonly IDatabaseObjectInspector _inspector;
    private readonly IConfiguration _configuration;

    public ProjectStatusProvider(IDatabaseObjectInspector inspector, IConfiguration configuration)
    {
        _inspector = inspector;
        _configuration = configuration;
    }

    public async Task<ProjectStatusResponse> GetAsync(CancellationToken cancellationToken = default)
    {
        var manifestCount = ReadManifestCount();
        int? applied = null;
        var databaseStatus = "Indisponível";
        var errors = new List<string>();
        try
        {
            applied = await _inspector.CountRowsAsync("sigov", "schema_migrations", cancellationToken).ConfigureAwait(false);
            databaseStatus = applied.HasValue ? "Conectado" : "Conectado; histórico de migrations ausente";
        }
        catch (Exception)
        {
            errors.Add("Banco não acessível no instante da consulta; consulte os logs operacionais.");
        }

        var advancedModules = await InspectAdvancedModulesAsync(cancellationToken).ConfigureAwait(false);
        var implemented = new[] { "Tenants e usuários", "Permissões e auditoria", "Educação/RH/Folha", "Financeiro/SIAFIC", "Compras/Contratos/Patrimônio", "Saúde/Assistência Social", "Saneamento/Frotas/Obras" }
            .Select(name => new ProjectModuleStatus(name, "Núcleo implementado"))
            .Concat(advancedModules.Where(module => module.Status == "Aplicado"))
            .ToArray();
        var pending = new[] { "Bloco 8 digital", "Bloco 9 empresarial", "Tributário avançado", "Saneamento avançado" }
            .Select(name => new ProjectModuleStatus(name, "Parcial"))
            .Concat(advancedModules.Where(module => module.Status != "Aplicado"))
            .ToArray();
        var priorities = new Dictionary<string, IReadOnlyCollection<string>>
        {
            ["P0"] = errors.Count == 0 ? Array.Empty<string>() : new[] { "Validar conectividade e migrations no ambiente real" },
            ["P1"] = new[] { "Fechar fluxos demonstráveis dos blocos 8 e 9" },
            ["P2"] = new[] { "Completar relatórios, exportações, LGPD e auditoria" }
        };

        return new ProjectStatusResponse(DateTimeOffset.UtcNow, databaseStatus,
            _configuration.GetValue("Sigov:Security:SwaggerEnabledInProduction", false) ? "Habilitado por configuração" : "Disponível em Development",
            "Não aferido em runtime; consultar pipeline/artefato de build", manifestCount, applied,
            applied.HasValue ? Math.Max(0, manifestCount - applied.Value) : null, implemented, pending, errors, priorities,
            new[] { "Validar RC50.48 — Educação Avançada em runtime", "Validar RC50.49 — Saúde Avançada em runtime", "Fechar relatórios e auditoria dos blocos 8 e 9" });
    }

    private async Task<IReadOnlyCollection<ProjectModuleStatus>> InspectAdvancedModulesAsync(CancellationToken cancellationToken)
    {
        var modules = new[]
        {
            (Name: "Educação avançada", Tables: new[] { "educacao_transporte_rota", "educacao_merenda_produto", "educacao_biblioteca_acervo", "educacao_indicador" }),
            (Name: "Saúde avançada", Tables: new[] { "saude_acs_lote_offline", "saude_visita_domiciliar", "saude_vacinacao_evento", "saude_farmacia_estoque", "saude_regulacao_fila" })
        };
        var statuses = new List<ProjectModuleStatus>(modules.Length);
        foreach (var module in modules)
        {
            try
            {
                var checks = await Task.WhenAll(module.Tables.Select(table => _inspector.TableExistsAsync("sigov", table, cancellationToken))).ConfigureAwait(false);
                var available = checks.Count(exists => exists);
                statuses.Add(new ProjectModuleStatus(module.Name, available == module.Tables.Length ? "Aplicado" : $"Pendente ({available}/{module.Tables.Length} tabelas)"));
            }
            catch (Exception)
            {
                statuses.Add(new ProjectModuleStatus(module.Name, "Pendente de validação do banco"));
            }
        }
        return statuses;
    }

    private static int ReadManifestCount()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var path = Path.Combine(directory.FullName, "database", "postgres", "migrations", "manifest.json");
            if (File.Exists(path))
            {
                using var document = JsonDocument.Parse(File.ReadAllText(path));
                return document.RootElement.GetProperty("migrations").GetArrayLength();
            }
            directory = directory.Parent;
        }
        return 0;
    }
}
