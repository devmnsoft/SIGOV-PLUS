using Microsoft.Extensions.DependencyInjection;
using Sigov.Application.Abstractions;
using Sigov.Infrastructure.Persistence.Dapper;
using Sigov.Infrastructure.Persistence.Migrations;
using Sigov.Infrastructure.Persistence.Repositories;
using Sigov.Infrastructure.Persistence.UnitOfWork;
using Sigov.Infrastructure.Security;

namespace Sigov.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services)
    {
        services.AddSingleton<NpgsqlConnectionFactory>();
        services.AddScoped<DapperContext>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<MigrationRunner>();
        services.AddScoped<PessoaRepository>();
        services.AddScoped<UsuarioRepository>();
        services.AddScoped<AuditRepository>();
        services.AddScoped<ILgpdMaskingService, LgpdMaskingService>();
        return services;
    }
}
