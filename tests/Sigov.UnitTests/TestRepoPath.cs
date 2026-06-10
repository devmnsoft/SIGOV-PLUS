namespace Sigov.Testing;

public static class TestRepoPath
{
    public static string Root
    {
        get
        {
            var directory = new DirectoryInfo(AppContext.BaseDirectory);

            while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "sigov.sln")))
            {
                directory = directory.Parent;
            }

            return directory?.FullName ?? Directory.GetCurrentDirectory();
        }
    }

    public static string Get(string relativePath)
    {
        return Path.Combine(Root, relativePath);
    }
}
