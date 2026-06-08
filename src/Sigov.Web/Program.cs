using Serilog;
using Sigov.Application.BusinessRules;
using Sigov.Application.Commercial;
using Sigov.Application.Demo;
using Sigov.Application.Executive;
using Sigov.Application.Onboarding;
using Sigov.Application.Ui;
using Sigov.Web.Branding;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, configuration) => configuration
    .ReadFrom.Configuration(context.Configuration)
    .Enrich.WithProperty("Application", "sigov")
    .Enrich.FromLogContext()
    .WriteTo.Console());

builder.Services.AddControllersWithViews();
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

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
}

app.UseStaticFiles();
app.UseRouting();
app.MapControllerRoute("default", "{controller=Home}/{action=Index}/{id?}");
app.Run();
