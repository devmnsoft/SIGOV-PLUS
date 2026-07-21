namespace Sigov.Testing;

public static class TestRepoPath
{
    public static string Root { get; } = FindRoot(AppContext.BaseDirectory);

    public static string Get(string relativePath) => Path.Combine(Root, relativePath);

    private static string FindRoot(string start)
    {
        var directory = new DirectoryInfo(start);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "sigov.sln")) && Directory.Exists(Path.Combine(directory.FullName, "src")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException($"Repository root not found from '{start}'.");
    }
}
