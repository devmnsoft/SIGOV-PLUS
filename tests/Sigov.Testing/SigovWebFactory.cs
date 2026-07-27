extern alias SigovWeb;

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace Sigov.Testing;

/// <summary>Web host using appsettings.Testing.json and isolated temporary storage.</summary>
public sealed class SigovWebFactory : WebApplicationFactory<SigovWeb::Program>
{
    private readonly string _storagePath = Path.Combine(Path.GetTempPath(), "sigov-web-tests", Guid.NewGuid().ToString("N"));

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        Directory.CreateDirectory(_storagePath);
        builder.UseEnvironment("Testing");
        builder.UseContentRoot(TestRepoPath.Get("src/Sigov.Web"));
        builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(SigovApiFactory.Settings(_storagePath)));
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing && Directory.Exists(_storagePath)) Directory.Delete(_storagePath, true);
    }
}
