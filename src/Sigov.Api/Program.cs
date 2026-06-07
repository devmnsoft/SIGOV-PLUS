using Microsoft.OpenApi.Models;
using Serilog;
using Sigov.Api.Middlewares;
using Microsoft.Extensions.Options;
using Sigov.Application.Configuration;
using Sigov.Infrastructure;
using Sigov.Infrastructure.Persistence.Migrations;
using Sigov.Application.BusinessRules;
using Sigov.Application.Commercial;
using Sigov.Application.Demo;
using Sigov.Application.Executive;
using Sigov.Application.Onboarding;
using Sigov.Application.Ui;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, configuration) => configuration
    .ReadFrom.Configuration(context.Configuration)
    .Enrich.WithProperty("Application", "sigov")
    .Enrich.FromLogContext()
    .WriteTo.Console());

builder.Services.AddOptions<SigovOptions>()
    .Bind(builder.Configuration.GetSection("Sigov"))
    .ValidateOnStart();
builder.Services.AddSingleton<IValidateOptions<SigovOptions>, SigovOptionsValidator>();
builder.Services.AddHttpContextAccessor();
builder.Services.AddControllers();
builder.Services.Configure<DemoModeOptions>(builder.Configuration.GetSection("Sigov:DemoMode"));
builder.Services.AddSingleton<IModuleCatalogService, ModuleCatalogService>();
builder.Services.AddSingleton<IBusinessRuleCatalog, BusinessRuleCatalog>();
builder.Services.AddSingleton<IBusinessRuleEvaluator, BusinessRuleEvaluator>();
builder.Services.AddSingleton<IOnboardingService, OnboardingService>();
builder.Services.AddSingleton<IDemoModeService, DemoModeService>();
builder.Services.AddSingleton<IUserPreferenceService, UserPreferenceService>();
builder.Services.AddSingleton<IUserSavedFilterService, UserSavedFilterService>();
builder.Services.AddSingleton<IExecutiveDashboardService, ExecutiveDashboardService>();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo { Title = "sigov API", Version = "v1" });
});
builder.Services.AddCors(options =>
{
    options.AddPolicy("SigovCors", policy =>
    {
        var origins = builder.Configuration.GetSection("Sigov:Security:CorsAllowedOrigins").Get<string[]>() ?? Array.Empty<string>();
        if (origins.Length > 0)
        {
            policy.WithOrigins(origins).AllowAnyHeader().AllowAnyMethod();
        }
    });
});
builder.Services.AddInfrastructure();

var app = builder.Build();

app.UseMiddleware<CorrelationIdMiddleware>();
app.UseMiddleware<ExceptionHandlingMiddleware>();
app.UseMiddleware<SecurityHeadersMiddleware>();
app.UseMiddleware<RequestLoggingMiddleware>();
app.UseMiddleware<TenantResolutionMiddleware>();
app.UseCors("SigovCors");
app.UseMiddleware<SimpleRateLimitMiddleware>();

var sigovOptions = app.Services.GetRequiredService<IOptions<SigovOptions>>().Value;
if (app.Environment.IsProduction() && string.IsNullOrWhiteSpace(app.Configuration.GetConnectionString("DefaultConnection")))
{
    throw new InvalidOperationException("ConnectionStrings:DefaultConnection deve ser fornecida por variável de ambiente/secret manager em Production.");
}

if (app.Configuration.GetValue("Sigov:Database:RunMigrationsOnStartup", false))
{
    using var scope = app.Services.CreateScope();
    var runner = scope.ServiceProvider.GetRequiredService<MigrationRunner>();
    await runner.RunAsync().ConfigureAwait(false);
}

if (app.Environment.IsDevelopment() || sigovOptions.Security.SwaggerEnabledInProduction)
{
    if (app.Environment.IsProduction())
    {
        app.Use(async (context, next) =>
        {
            if (context.Request.Path.StartsWithSegments("/swagger", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(context.Request.Headers["X-Sigov-Bootstrap-Token"].ToString(), sigovOptions.Security.BootstrapToken, StringComparison.Ordinal))
            {
                context.Response.StatusCode = StatusCodes.Status404NotFound;
                return;
            }

            await next().ConfigureAwait(false);
        });
    }

    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapControllers();

app.Run();

public partial class Program
{
}
