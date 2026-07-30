using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;

namespace Sigov.Testing;

public sealed class TestHostEnvironment : IHostEnvironment, IWebHostEnvironment
{
    public TestHostEnvironment(string? contentRootPath = null, string? webRootPath = null)
    {
        ContentRootPath = Path.GetFullPath(contentRootPath ?? Directory.GetCurrentDirectory());
        WebRootPath = Path.GetFullPath(webRootPath ?? ResolveWebRoot(ContentRootPath));

        if (!Directory.Exists(ContentRootPath))
        {
            throw new DirectoryNotFoundException($"The test content root does not exist: {ContentRootPath}");
        }

        if (!Directory.Exists(WebRootPath))
        {
            throw new DirectoryNotFoundException($"The test web root does not exist: {WebRootPath}");
        }

        ContentRootFileProvider = new PhysicalFileProvider(ContentRootPath);
        WebRootFileProvider = new PhysicalFileProvider(WebRootPath);
    }

    public string EnvironmentName { get; set; } = "Testing";

    public string ApplicationName { get; set; } = "SIGOV PLUS Tests";

    public string ContentRootPath { get; set; }

    public IFileProvider ContentRootFileProvider { get; set; }

    public string WebRootPath { get; set; }

    public IFileProvider WebRootFileProvider { get; set; }

    private static string ResolveWebRoot(string contentRootPath)
    {
        var webRoot = Path.Combine(contentRootPath, "src", "Sigov.Web", "wwwroot");
        return Directory.Exists(webRoot) ? webRoot : contentRootPath;
    }
}
