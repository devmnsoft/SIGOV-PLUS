using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Sigov.Application.Abstractions;
using Sigov.Application.Enterprise;
using Sigov.Application.Operational;
using Sigov.Application.Saas;
using Sigov.Application.Integracoes;
using Sigov.Application.Health;
using Sigov.Worker.Outbox.Handlers;
using Sigov.Infrastructure;
using Sigov.Infrastructure.Common;
using Sigov.Infrastructure.Persistence.Dapper;
using Sigov.Infrastructure.Persistence.Migrations;
using Sigov.Web.Services;
using Sigov.Web.Services.Operational;
using Sigov.Worker.Outbox;
using Sigov.Testing;
using Sigov.Web;

namespace Sigov.UnitTests;

public sealed class DependencyInjectionResolutionTests
{
    [Fact]
    public void Infrastructure_container_resolves_core_runtime_services_without_cycles()
    {
        var services = CreateBaseServices().AddInfrastructure();
        using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });
        using var scope = provider.CreateScope();

        scope.ServiceProvider.GetRequiredService<EnterpriseDapperCrudService>().Should().NotBeNull();
        scope.ServiceProvider.GetRequiredService<IEnterpriseModuleService>().Should().NotBeNull();
        scope.ServiceProvider.GetRequiredService<IEnterpriseCrudService>().Should().NotBeNull();
        scope.ServiceProvider.GetRequiredService<NpgsqlConnectionFactory>().Should().NotBeNull();
        scope.ServiceProvider.GetRequiredService<DapperContext>().Should().NotBeNull();
        scope.ServiceProvider.GetRequiredService<MigrationRunner>().Should().NotBeNull();
        scope.ServiceProvider.GetRequiredService<IPasswordHashService>().Should().NotBeNull();
        scope.ServiceProvider.GetRequiredService<ITenantResolver>().Should().NotBeNull();
        scope.ServiceProvider.GetRequiredService<IAuditService>().Should().NotBeNull();
        scope.ServiceProvider.GetRequiredService<IOutboxService>().Should().NotBeNull();
        scope.ServiceProvider.GetRequiredService<ITarefaService>().Should().NotBeNull();
        scope.ServiceProvider.GetRequiredService<IAgendaService>().Should().NotBeNull();
        scope.ServiceProvider.GetRequiredService<IPrazoOperacionalService>().Should().NotBeNull();
        scope.ServiceProvider.GetRequiredService<INotificacaoService>().Should().NotBeNull();
        scope.ServiceProvider.GetRequiredService<INotificacaoPreferenceService>().Should().NotBeNull();
        scope.ServiceProvider.GetRequiredService<IKanbanService>().Should().NotBeNull();
        scope.ServiceProvider.GetRequiredService<IOperationalEventPublisher>().Should().NotBeNull();
        scope.ServiceProvider.GetServices<IHealthCheck>().Should().NotBeEmpty();
    }

    [Fact]
    public void Enterprise_interfaces_share_same_scoped_instance()
    {
        var services = CreateBaseServices().AddInfrastructure();
        using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });
        using var scope = provider.CreateScope();

        var module = scope.ServiceProvider.GetRequiredService<IEnterpriseModuleService>();
        var crud = scope.ServiceProvider.GetRequiredService<IEnterpriseCrudService>();

        module.Should().BeSameAs(crud);

        using var otherScope = provider.CreateScope();
        otherScope.ServiceProvider.GetRequiredService<IEnterpriseModuleService>().Should().NotBeSameAs(module);
    }

    [Fact]
    public void Api_style_container_has_single_enterprise_contract_registration()
    {
        var services = CreateBaseServices().AddInfrastructure();

        services.Count(d => d.ServiceType == typeof(EnterpriseDapperCrudService)).Should().Be(1);
        services.Count(d => d.ServiceType == typeof(IEnterpriseModuleService)).Should().Be(1);
        services.Count(d => d.ServiceType == typeof(IEnterpriseCrudService)).Should().Be(1);
    }

    [Fact]
    public void Web_operational_services_resolve_with_infrastructure_services()
    {
        var services = CreateBaseServices().AddInfrastructure();
        services.AddSigovWebOperationalServices();

        using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });
        using var scope = provider.CreateScope();

        scope.ServiceProvider.GetRequiredService<IDatabaseSchemaInspector>().Should().NotBeNull();
        scope.ServiceProvider.GetRequiredService<ITenantContextAccessor>().Should().NotBeNull();
        scope.ServiceProvider.GetRequiredService<MinhaCentralService>().Should().NotBeNull();
        scope.ServiceProvider.GetRequiredService<PostBuildSaasService>().Should().NotBeNull();
        scope.ServiceProvider.GetRequiredService<OperationalDemoService>().Should().NotBeNull();
        scope.ServiceProvider.GetRequiredService<IOperationalStatusService>().Should().NotBeNull();
        scope.ServiceProvider.GetRequiredService<OutboxSigovService>().Should().NotBeNull();
    }

    [Fact]
    public void Worker_outbox_services_resolve_when_registered()
    {
        var services = CreateBaseServices().AddInfrastructure();
        services.AddScoped<Sigov.Infrastructure.Outbox.IOutboxRepository, Sigov.Infrastructure.Outbox.OutboxRepository>();
        services.AddScoped<IOutboxRetryPolicy, OutboxRetryPolicy>();
        services.AddScoped<IOutboxHandlerFactory, OutboxHandlerFactory>();
        services.AddScoped<IOutboxProcessor, OutboxProcessor>();
        services.AddScoped<IOutboxJob, OutboxJob>();
        services.AddScoped<IOutboxHandler, DefaultOutboxHandler>();

        using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });
        using var scope = provider.CreateScope();

        scope.ServiceProvider.GetRequiredService<IOutboxJob>().Should().NotBeNull();
        scope.ServiceProvider.GetRequiredService<IOutboxProcessor>().Should().NotBeNull();
        scope.ServiceProvider.GetServices<IOutboxHandler>().Should().NotBeEmpty();
    }

    private static IServiceCollection CreateBaseServices()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = "Host=localhost;Port=5432;Database=sigov;Username=sigov;Password=sigov_ci_password",
                ["Sigov:Database:Schema"] = "sigov",
                ["Sigov:Storage:LocalPath"] = "storage/test"
            })
            .Build();

        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddLogging();
        services.AddOptions();
        services.AddHttpContextAccessor();
        services.AddSigovTestHostEnvironment();
        return services;
    }
}
