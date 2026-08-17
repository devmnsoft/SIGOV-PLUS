namespace Sigov.Application.Health;

public sealed record ProjectModuleStatus(string Name, string Status);

public sealed record ProjectStatusResponse(
    DateTimeOffset GeneratedAt,
    string DatabaseStatus,
    string SwaggerStatus,
    string BuildStatus,
    int ManifestMigrations,
    int? AppliedMigrations,
    int? PendingMigrations,
    IReadOnlyCollection<ProjectModuleStatus> ImplementedModules,
    IReadOnlyCollection<ProjectModuleStatus> PendingModules,
    IReadOnlyCollection<string> KnownErrors,
    IReadOnlyDictionary<string, IReadOnlyCollection<string>> PendingByPriority,
    IReadOnlyCollection<string> SuggestedPrompts);
