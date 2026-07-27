using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.FileProviders;

namespace Sigov.Testing;

/// <summary>A deterministic host environment for tests that must not inherit developer settings.</summary>
public sealed class TestHostEnvironment : IWebHostEnvironment
{
    public TestHostEnvironment(string contentRootPath, string? webRootPath = null)
    {
        ApplicationName = "SIGOV PLUS Tests";
        EnvironmentName = "Testing";
        ContentRootPath = Path.GetFullPath(contentRootPath);
        ContentRootFileProvider = new PhysicalFileProvider(ContentRootPath);
        WebRootPath = Path.GetFullPath(webRootPath ?? Path.Combine(ContentRootPath, "wwwroot"));
        WebRootFileProvider = Directory.Exists(WebRootPath)
            ? new PhysicalFileProvider(WebRootPath)
            : new NullFileProvider();
    }

    public string ApplicationName { get; set; }
    public string EnvironmentName { get; set; }
    public string ContentRootPath { get; set; }
    public IFileProvider ContentRootFileProvider { get; set; }
    public string WebRootPath { get; set; }
    public IFileProvider WebRootFileProvider { get; set; }
}
