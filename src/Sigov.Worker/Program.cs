using Serilog;
using Sigov.Worker;

var builder = Host.CreateDefaultBuilder(args)
    .UseSerilog((context, configuration) => configuration
        .ReadFrom.Configuration(context.Configuration)
        .Enrich.WithProperty("Application", "sigov")
        .Enrich.FromLogContext()
        .WriteTo.Console())
    .ConfigureServices(services => services.AddHostedService<Worker>());

await builder.RunConsoleAsync().ConfigureAwait(false);
