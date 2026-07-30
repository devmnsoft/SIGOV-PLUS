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
using Sigov.Web.Services.Editais;
using Sigov.Web;

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
builder.Services.AddAuthorization(options =>
{
    string[] osPermissions = ["os.dashboard.visualizar", "os.ordens.visualizar", "os.ordens.agendar"];
    foreach (var permission in osPermissions)
        options.AddPolicy(permission, policy => policy.RequireAssertion(context =>
            context.User.IsInRole("ADMIN_GERAL") || context.User.IsInRole("ADMIN_TENANT") ||
            context.User.Claims.Where(c => c.Type is "permission" or "permissions" or "scope")
                .SelectMany(c => c.Value.Split([' ', ',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                .Contains(permission, StringComparer.OrdinalIgnoreCase))));
});
builder.Services.AddHttpContextAccessor();
builder.Services.Configure<SigovBrandOptions>(builder.Configuration.GetSection("Sigov:Brand"));
builder.Services.Configure<DemoModeOptions>(builder.Configuration.GetSection("Sigov:DemoMode"));
builder.Services.AddSingleton<ISigovBrandProvider, SigovBrandProvider>();
builder.Services.AddSingleton<ITenantBrandingProvider, TenantBrandingProvider>();
builder.Services.AddSingleton<IModuleCatalogService, ModuleCatalogService>();
builder.Services.AddSingleton<IBusinessRuleCatalog, BusinessRuleCatalog>();
builder.Services.AddSingleton<IBusinessRuleEvaluator, BusinessRuleEvaluator>();
builder.Services.AddSingleton<IOnboardingService, OnboardingService>();
builder.Services.AddSingleton<IDemoModeService, DemoModeService>();
builder.Services.AddSingleton<IUserPreferenceService, UserPreferenceService>();
builder.Services.AddSingleton<IUserSavedFilterService, UserSavedFilterService>();
builder.Services.AddSingleton<IExecutiveDashboardService, ExecutiveDashboardService>();
builder.Services.AddSingleton<Sigov.Application.Saas.Modules.IModuleCatalogService, Sigov.Application.Saas.Modules.ModuleCatalogService>();
builder.Services.AddInfrastructure();
builder.Services.AddSigovWebOperationalServices();
builder.Services.AddScoped<SegurancaAdminService>();
builder.Services.AddScoped<IAuditTrailService, AuditTrailService>();
builder.Services.AddScoped<IUserPermissionService, UserPermissionService>();
builder.Services.AddScoped<IMenuAuthorizationService, MenuAuthorizationService>();
builder.Services.AddScoped<SectorModuleService>();
builder.Services.AddScoped<ProtocoloOperationalService>();
builder.Services.AddScoped<GedOperationalService>();
builder.Services.AddScoped<TributarioOperationalService>();
builder.Services.AddScoped<ContratosOperationalService>();
builder.Services.AddScoped<JuridicoOperationalService>();
builder.Services.AddScoped<FinanceiroOperationalService>();
builder.Services.AddScoped<SiaficService>();
builder.Services.AddScoped<PlanejamentoService>();
builder.Services.AddScoped<TesourariaService>();
builder.Services.AddScoped<ComprasService>();
builder.Services.AddScoped<LicitacoesService>();
builder.Services.AddScoped<AlmoxarifadoService>();
builder.Services.AddScoped<PatrimonioService>();
builder.Services.AddScoped<InventarioService>();
builder.Services.AddScoped<FrotasService>();
builder.Services.AddScoped<ObrasService>();
builder.Services.AddScoped<TransparenciaService>();
builder.Services.AddScoped<OperationalEventService>();
builder.Services.AddScoped<WorkflowService>();
builder.Services.AddScoped<WorkflowDefinitionService>();
builder.Services.AddScoped<WorkflowInstanceService>();
builder.Services.AddScoped<TarefaService>();
builder.Services.AddScoped<NotificacaoService>();
builder.Services.AddScoped<AgendaOperacionalService>();
builder.Services.AddScoped<IntegracaoMonitorService>();
builder.Services.AddScoped<BiOperacionalService>();
builder.Services.AddScoped<MobileCampoService>();
builder.Services.AddScoped<AiConfigurationService>();
builder.Services.AddScoped<AiAssistantService>();
builder.Services.AddScoped<AiAuditService>();
builder.Services.AddScoped<ImplantacaoService>();
builder.Services.AddScoped<ImplantacaoEtapaService>();
builder.Services.AddScoped<ImplantacaoEvidenciaService>();
builder.Services.AddScoped<MigracaoService>();
builder.Services.AddScoped<MigracaoValidacaoService>();
builder.Services.AddScoped<TreinamentoService>();
builder.Services.AddScoped<CertificadoTreinamentoService>();
builder.Services.AddScoped<SuporteService>();
builder.Services.AddScoped<SlaService>();
builder.Services.AddScoped<SlaMonitorService>();
builder.Services.AddScoped<PocService>();
builder.Services.AddScoped<PocRoteiroService>();
builder.Services.AddScoped<PocEvidenciaService>();
builder.Services.AddScoped<AceiteFormalService>();
builder.Services.AddScoped<EditalPocService>();
builder.Services.AddScoped<PosRcWebOperationalService>();

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

public partial class Program { }
