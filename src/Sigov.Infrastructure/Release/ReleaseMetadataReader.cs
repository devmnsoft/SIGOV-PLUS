namespace Sigov.Infrastructure.Release;

public sealed class ReleaseMetadataReader
{
    public string? ReadVersion(string contentRootPath)
    {
        var path = Path.Combine(contentRootPath, "VERSION");
        return File.Exists(path) ? File.ReadAllText(path).Trim() : null;
    }
}
