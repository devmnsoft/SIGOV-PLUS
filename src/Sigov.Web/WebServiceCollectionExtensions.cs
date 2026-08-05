using Microsoft.Extensions.DependencyInjection;
using Sigov.Web.Services;
using Sigov.Web.Services.Operational;

namespace Sigov.Web;

public static class WebServiceCollectionExtensions
{
    public static IServiceCollection AddSigovWebOperationalServices(this IServiceCollection services)
    {
        services.AddScoped<IDatabaseSchemaInspector, DatabaseSchemaInspector>();
        services.AddScoped<ITenantContextAccessor, TenantContextAccessor>();
        services.AddScoped<PostBuildSaasService>();
        services.AddScoped<MinhaCentralService>();
        services.AddScoped<BuscaGlobalService>();
        services.AddScoped<OperationalDemoService>();
        services.AddScoped<IOperationalStatusService, OperationalStatusService>();
        services.AddScoped<OutboxSigovService>();
        return services;
    }
}
