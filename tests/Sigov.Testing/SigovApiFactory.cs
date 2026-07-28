extern alias SigovApi;

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace Sigov.Testing;

/// <summary>API host with production middleware and isolated, database-free Testing configuration.</summary>
public sealed class SigovApiFactory : WebApplicationFactory<SigovApi::Sigov.Api.ApiEntryPointMarker>
{
    private readonly string _storagePath = Path.Combine(Path.GetTempPath(), "sigov-tests", Guid.NewGuid().ToString("N"));

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        Directory.CreateDirectory(_storagePath);
        builder.UseEnvironment("Testing");
        builder.UseContentRoot(TestRepoPath.Get("src/Sigov.Api"));
        builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(Settings(_storagePath)));
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing && Directory.Exists(_storagePath)) Directory.Delete(_storagePath, true);
    }

    internal static IReadOnlyDictionary<string, string?> Settings(string storagePath) => new Dictionary<string, string?>
    {
        ["Sigov:Database:MigrationMode"] = "Disabled",
        ["Sigov:Database:RunMigrationsOnStartup"] = "false",
        ["Sigov:Seed:Demo"] = "false",
        ["Sigov:DemoMode:Enabled"] = "false",
        ["Sigov:Worker:Enabled"] = "false",
        ["Sigov:Storage:RootPath"] = storagePath
    };
}
