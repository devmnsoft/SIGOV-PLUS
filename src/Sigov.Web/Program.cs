using Serilog;
using Sigov.Application.BusinessRules;
using Sigov.Application.Commercial;
using Sigov.Application.Enterprise;
using Sigov.Application.Demo;
using Sigov.Application.Executive;
using Sigov.Application.Onboarding;
using Sigov.Application.Ui;
using Microsoft.AspNetCore.Authentication.Cookies;
using Serilog.Context;
using Sigov.Infrastructure;
using Sigov.Web.Branding;
using Sigov.Web.Services;
using Sigov.Web.Services.Operational;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, configuration) => configuration
    .ReadFrom.Configuration(context.Configuration)
    .Enrich.WithProperty("Application", "sigov")
    .Enrich.FromLogContext()
    .WriteTo.Console());

builder.Services.AddControllersWithViews();
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Auth/Login";
        options.LogoutPath = "/Auth/Logout";
        options.AccessDeniedPath = "/Auth/Login";
        options.Cookie.Name = "SIGOV.AUTH";
        options.SlidingExpiration = true;
    });
builder.Services.AddAuthorization();
builder.Services.AddHttpContextAccessor();
builder.Services.Configure<SigovBrandOptions>(builder.Configuration.GetSection("Sigov:Brand"));
builder.Services.Configure<DemoModeOptions>(builder.Configuration.GetSection("Sigov:DemoMode"));
builder.Services.AddSingleton<ISigovBrandProvider, SigovBrandProvider>();
builder.Services.AddSingleton<ITenantBrandingProvider, TenantBrandingProvider>();
builder.Services.AddSingleton<IModuleCatalogService, ModuleCatalogService>();
builder.Services.AddSingleton<IEnterpriseModuleService, EnterpriseModuleService>();
builder.Services.AddSingleton<IBusinessRuleCatalog, BusinessRuleCatalog>();
builder.Services.AddSingleton<IBusinessRuleEvaluator, BusinessRuleEvaluator>();
builder.Services.AddSingleton<IOnboardingService, OnboardingService>();
builder.Services.AddSingleton<IDemoModeService, DemoModeService>();
builder.Services.AddSingleton<IUserPreferenceService, UserPreferenceService>();
builder.Services.AddSingleton<IUserSavedFilterService, UserSavedFilterService>();
builder.Services.AddSingleton<IExecutiveDashboardService, ExecutiveDashboardService>();
builder.Services.AddSingleton<Sigov.Application.Saas.Modules.IModuleCatalogService, Sigov.Application.Saas.Modules.ModuleCatalogService>();
builder.Services.AddInfrastructure();
builder.Services.AddScoped<IDatabaseSchemaInspector, DatabaseSchemaInspector>();
builder.Services.AddScoped<PostBuildSaasService>();
builder.Services.AddScoped<SegurancaAdminService>();
builder.Services.AddScoped<MinhaCentralService>();
builder.Services.AddScoped<IAuditTrailService, AuditTrailService>();
builder.Services.AddScoped<IUserPermissionService, UserPermissionService>();
builder.Services.AddScoped<IMenuAuthorizationService, MenuAuthorizationService>();
builder.Services.AddScoped<OperationalDemoService>();
builder.Services.AddScoped<ProtocoloOperationalService>();
builder.Services.AddScoped<GedOperationalService>();
builder.Services.AddScoped<TributarioOperationalService>();
builder.Services.AddScoped<ContratosOperationalService>();
builder.Services.AddScoped<JuridicoOperationalService>();
builder.Services.AddScoped<FinanceiroOperationalService>();
builder.Services.AddScoped<IOperationalStatusService, OperationalStatusService>();
builder.Services.AddScoped<OperationalEventService>();
builder.Services.AddScoped<OutboxSigovService>();
builder.Services.AddScoped<WorkflowService>();
builder.Services.AddScoped<WorkflowDefinitionService>();
builder.Services.AddScoped<WorkflowInstanceService>();
builder.Services.AddScoped<TarefaService>();
builder.Services.AddScoped<NotificacaoService>();
builder.Services.AddScoped<AgendaOperacionalService>();
builder.Services.AddScoped<IntegracaoMonitorService>();
builder.Services.AddScoped<BiOperacionalService>();
builder.Services.AddScoped<MobileCampoService>();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
}

app.UseStatusCodePagesWithReExecute("/Home/Error/{0}");
app.UseStaticFiles();
app.UseRouting();
app.Use(async (context, next) =>
{
    var correlationId = context.Request.Headers.TryGetValue("X-Correlation-Id", out var header) && Guid.TryParse(header, out var parsed)
        ? parsed
        : Guid.NewGuid();
    context.TraceIdentifier = correlationId.ToString();
    context.Response.Headers["X-Correlation-Id"] = correlationId.ToString();
    using (LogContext.PushProperty("CorrelationId", correlationId))
    {
        await next().ConfigureAwait(false);
    }
});
app.UseAuthentication();
app.UseAuthorization();
app.MapControllerRoute("default", "{controller=Home}/{action=Index}/{id?}");
app.Run();
