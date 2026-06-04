using Serilog;
using SIGOV.Worker;

var builder = Host.CreateDefaultBuilder(args)
    .UseSerilog((context, configuration) => configuration
        .ReadFrom.Configuration(context.Configuration)
        .Enrich.FromLogContext()
        .WriteTo.Console())
    .ConfigureServices(services => services.AddHostedService<Worker>());

await builder.RunConsoleAsync().ConfigureAwait(false);
