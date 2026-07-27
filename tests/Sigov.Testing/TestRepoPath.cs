namespace Sigov.Testing;

/// <summary>Compatibility facade for tests migrated to <see cref="RepositoryPathResolver"/>.</summary>
public static class TestRepoPath
{
    private static readonly Lazy<RepositoryPathResolver> Resolver = new(() => new RepositoryPathResolver());

    public static string Root => Resolver.Value.RepoRoot;

    public static string Get(string relativePath) => Resolver.Value.Resolve(relativePath);
}
