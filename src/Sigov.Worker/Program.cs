using Serilog;
using Sigov.Infrastructure;
using Sigov.Worker;
using Sigov.Worker.Outbox;
using Sigov.Worker.Outbox.Handlers;

var builder = Host.CreateDefaultBuilder(args)
    .UseSerilog((context, configuration) => configuration
        .ReadFrom.Configuration(context.Configuration)
        .Enrich.WithProperty("Application", "sigov")
        .Enrich.FromLogContext()
        .WriteTo.Console())
    .ConfigureServices(services =>
    {
        services.AddInfrastructure();
        services.AddScoped<Sigov.Infrastructure.Outbox.IOutboxRepository, Sigov.Infrastructure.Outbox.OutboxRepository>();
        services.AddScoped<IOutboxRetryPolicy, OutboxRetryPolicy>();
        services.AddScoped<IOutboxHandlerFactory, OutboxHandlerFactory>();
        services.AddScoped<IOutboxProcessor, OutboxProcessor>();
        services.AddScoped<IOutboxJob, OutboxJob>();
        services.AddScoped<IOutboxHandler, WebhookOutboxHandler>();
        services.AddScoped<IOutboxHandler, IntegracaoOutboxHandler>();
        services.AddScoped<IOutboxHandler, RelatorioOutboxHandler>();
        services.AddScoped<IOutboxHandler, FinanceiroOutboxHandler>();
        services.AddScoped<IOutboxHandler, SuporteOutboxHandler>();
        services.AddScoped<IOutboxHandler, DefaultOutboxHandler>();
        services.AddHostedService<Worker>();
    });

await builder.RunConsoleAsync().ConfigureAwait(false);
