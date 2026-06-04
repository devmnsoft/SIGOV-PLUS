using Microsoft.Extensions.DependencyInjection;
using Sigov.Application.Abstractions;
using Sigov.Infrastructure.Common;
using Sigov.Infrastructure.Persistence.Dapper;
using Sigov.Infrastructure.Persistence.Migrations;
using Sigov.Infrastructure.Persistence.Repositories;
using Sigov.Infrastructure.Persistence.UnitOfWork;
using Sigov.Infrastructure.Security;
using Sigov.Application.Saas;
using Sigov.Infrastructure.Saas;
using Sigov.Application.Storage;
using Sigov.Infrastructure.Storage;

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
        services.AddScoped<IDateTimeProvider, DateTimeProvider>();
        services.AddScoped<ICorrelationIdProvider, CorrelationIdProvider>();
        services.AddScoped<ICurrentUser, CurrentUser>();
        services.AddScoped<ICurrentTenant, CurrentTenant>();
        services.AddScoped<ICurrentExercicio, CurrentExercicio>();
        services.AddScoped<IPasswordHashService, PasswordHashService>();
        services.AddScoped<IPermissionService, PermissionService>();
        services.AddScoped<ITenantContext, TenantContext>();
        services.AddScoped<ITenantResolver, TenantResolver>();
        services.AddScoped<ITenantProvisioningService, TenantProvisioningService>();
        services.AddScoped<IFeatureFlagService, FeatureFlagService>();
        services.AddScoped<IModuloLicenciamentoService, ModuloLicenciamentoService>();
        services.AddScoped<ITenantUsageMeter, TenantUsageMeter>();
        services.AddScoped<ITenantConfigurationProvider, TenantConfigurationProvider>();
        services.AddScoped<Func<long, CancellationToken, Task<string?>>>(provider => async (tenantId, cancellationToken) =>
        {
            var context = provider.GetRequiredService<DapperContext>();
            using var connection = context.CreateConnection();
            return await Dapper.SqlMapper.ExecuteScalarAsync<string?>(connection, new Dapper.CommandDefinition("select status from sigov.tenant where id = @TenantId and ativo = true and is_deleted = false;", new { TenantId = tenantId }, cancellationToken: cancellationToken)).ConfigureAwait(false);
        });
        services.AddScoped<ITenantAccessGuard, TenantAccessGuard>();
        services.AddScoped<IFileHashService, FileHashService>();
        services.AddScoped<IStorageKeyGenerator, StorageKeyGenerator>();
        services.AddScoped<IFileTypeValidator, FileTypeValidator>();
        services.AddScoped<IAntivirusScanner, NoOpAntivirusScanner>();
        services.AddScoped<IFileStorageService, LocalFileStorageService>();
        return services;
    }
}
