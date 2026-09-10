using System.Text.Json;
using Dapper;
using Sigov.Application.Saas.Modules;
using Sigov.Infrastructure.Persistence.Dapper;

namespace Sigov.Infrastructure.Saas;

public sealed class PersistentModuleCatalogService(DapperContext context) : IModuleCatalogService
{
    public async Task<IReadOnlyCollection<ModuleCatalogItem>> GetModulesAsync(CancellationToken cancellationToken = default)
    {
        using var connection = context.CreateConnection();
        IReadOnlyList<CatalogRow> rows;
        try
        {
            rows = (await connection.QueryAsync<CatalogRow>(new CommandDefinition(ModuleSql, cancellationToken: cancellationToken)).ConfigureAwait(false)).AsList();
        }
        catch (Exception exception)
        {
            throw new InvalidOperationException("Catálogo modulo_saas indisponível; o banco é a autoridade e a ausência não pode ser mascarada.", exception);
        }

        return rows.Select(MapModule).ToArray();
    }

    public async Task<ModuleCatalogItem?> FindByCodeAsync(string codigo, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(codigo))
            return null;
        var modules = await GetModulesAsync(cancellationToken).ConfigureAwait(false);
        return modules.FirstOrDefault(module => string.Equals(module.Codigo, codigo, StringComparison.OrdinalIgnoreCase));
    }

    public async Task<IReadOnlyCollection<ModulePackageItem>> GetPackagesAsync(CancellationToken cancellationToken = default)
    {
        using var connection = context.CreateConnection();
        IReadOnlyList<PackageRow> rows;
        try
        {
            rows = (await connection.QueryAsync<PackageRow>(new CommandDefinition(PackageSql, cancellationToken: cancellationToken)).ConfigureAwait(false)).AsList();
        }
        catch (Exception exception)
        {
            throw new InvalidOperationException("Pacotes tenant_modulo_pacote indisponíveis; o banco é a autoridade e a ausência não pode ser mascarada.", exception);
        }

        return rows.Select(MapPackage).ToArray();
    }

    public async Task<ModulePackageItem?> FindPackageByCodeAsync(string codigo, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(codigo))
            return null;
        var packages = await GetPackagesAsync(cancellationToken).ConfigureAwait(false);
        return packages.FirstOrDefault(package => string.Equals(package.Codigo, codigo, StringComparison.OrdinalIgnoreCase));
    }

    private static ModuleCatalogItem MapModule(CatalogRow row)
    {
        var dependencies = ParseStringArray(row.DependenciasJson);
        var features = ParseStringArray(row.RecursosJson)
            .Select(item => new ModuleFeatureItem(item, item, item))
            .DefaultIfEmpty(new ModuleFeatureItem($"{row.Codigo}.dashboard", "Dashboard", $"Painel do módulo {row.Nome}."))
            .ToArray();
        return new ModuleCatalogItem(
            row.Codigo,
            row.Nome,
            row.Descricao ?? string.Empty,
            row.Categoria ?? "Plataforma",
            true,
            true,
            dependencies,
            features,
            new[] { "Contratação por tenant", "Vigência e suspensão auditadas", "Dependências obrigatórias" },
            string.IsNullOrWhiteSpace(row.RotaBase) ? $"/{ToPascal(row.Codigo)}" : row.RotaBase,
            new[] { $"{row.Codigo}.visualizar", $"{row.Codigo}.gerenciar" });
    }

    private static ModulePackageItem MapPackage(PackageRow row) =>
        new(row.Codigo, row.Nome, row.Descricao ?? string.Empty, ParseStringArray(row.ModulosJson));

    private static IReadOnlyCollection<string> ParseStringArray(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return Array.Empty<string>();
        try
        {
            return JsonSerializer.Deserialize<string[]>(json) ?? Array.Empty<string>();
        }
        catch (JsonException)
        {
            return Array.Empty<string>();
        }
    }

    private static string ToPascal(string value) => string.Concat(value.Split('-', '_').Select(part =>
        part.Length == 0 ? string.Empty : char.ToUpperInvariant(part[0]) + part[1..]));

    private sealed record CatalogRow(string Codigo, string Nome, string? Descricao, string? Categoria, string? RotaBase, string? DependenciasJson, string? RecursosJson);
    private sealed record PackageRow(string Codigo, string Nome, string? Descricao, string? ModulosJson);

    private const string ModuleSql = """
select codigo as Codigo, nome as Nome, descricao as Descricao, categoria as Categoria, rota_base as RotaBase,
       coalesce(dependencias, '[]'::jsonb)::text as DependenciasJson,
       coalesce(recursos_incluidos, '[]'::jsonb)::text as RecursosJson
from sigov.modulo_saas
where ativo and not is_deleted
order by ordem, nome
""";

    private const string PackageSql = """
select codigo as Codigo, nome as Nome, descricao as Descricao, coalesce(modulos_json, '[]'::jsonb)::text as ModulosJson
from sigov.tenant_modulo_pacote
where ativo
order by nome
""";
}
