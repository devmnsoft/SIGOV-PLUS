using Microsoft.Extensions.DependencyInjection;
using SIGOV.Application.Abstractions;
using SIGOV.Infrastructure.Persistence.Dapper;
using SIGOV.Infrastructure.Persistence.UnitOfWork;
using SIGOV.Infrastructure.Security;

namespace SIGOV.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services)
    {
        services.AddSingleton<NpgsqlConnectionFactory>();
        services.AddScoped<DapperContext>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<ILgpdMaskingService, LgpdMaskingService>();
        return services;
    }
}
