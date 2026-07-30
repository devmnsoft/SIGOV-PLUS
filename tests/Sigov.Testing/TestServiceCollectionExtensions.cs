using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Sigov.Testing;

public static class TestServiceCollectionExtensions
{
    public static IServiceCollection AddSigovTestHostEnvironment(this IServiceCollection services)
    {
        var environment = new TestHostEnvironment();
        services.AddSingleton(environment);
        services.AddSingleton<IHostEnvironment>(environment);
        services.AddSingleton<IWebHostEnvironment>(environment);
        return services;
    }
}
