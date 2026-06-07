namespace Sigov.Testing;

public static class TestRepoPath
{
    public static string Get(string relativePath)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "sigov.sln")))
        {
            directory = directory.Parent;
        }

        return Path.Combine(directory?.FullName ?? Directory.GetCurrentDirectory(), relativePath);
    }
}
