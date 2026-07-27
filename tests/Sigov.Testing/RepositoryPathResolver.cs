namespace Sigov.Testing;

/// <summary>Resolves repository paths independently of the test runner's working directory.</summary>
public sealed class RepositoryPathResolver
{
    public RepositoryPathResolver(string? startPath = null)
    {
        var start = startPath ?? AppContext.BaseDirectory;
        var directory = new DirectoryInfo(Path.GetFullPath(start));
        if (!directory.Exists && directory.Parent is not null)
        {
            directory = directory.Parent;
        }

        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "sigov.sln")))
        {
            directory = directory.Parent;
        }

        RepoRoot = directory?.FullName
            ?? throw new DirectoryNotFoundException($"Não foi possível localizar sigov.sln a partir de '{start}'.");
    }

    public string RepoRoot { get; }
    public string SrcRoot => Path.Combine(RepoRoot, "src");
    public string TestsRoot => Path.Combine(RepoRoot, "tests");
    public string DatabaseRoot => Path.Combine(RepoRoot, "database");
    public string MigrationsRoot => Path.Combine(DatabaseRoot, "postgres", "migrations");
    public string ScriptsRoot => Path.Combine(RepoRoot, "scripts");
    public string DocsRoot => Path.Combine(RepoRoot, "docs");
    public string WebRoot => Path.Combine(SrcRoot, "Sigov.Web");
    public string ApiRoot => Path.Combine(SrcRoot, "Sigov.Api");
    public string WorkerRoot => Path.Combine(SrcRoot, "Sigov.Worker");

    public string Resolve(params string[] segments) =>
        segments.Aggregate(RepoRoot, Path.Combine);
}
