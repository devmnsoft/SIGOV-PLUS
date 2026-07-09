using System.Security.Claims;
using Microsoft.Extensions.Options;
using Sigov.Application.Demo;

namespace Sigov.Web.Services;

public interface ITenantContextAccessor
{
    TenantContext Resolve();
}

public sealed record TenantContext(long? TenantId, string TenantNome, bool IsGlobal, string DataSource, string? MensagemFallback);

public sealed class TenantContextAccessor : ITenantContextAccessor
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IConfiguration _configuration;
    private readonly IOptions<DemoModeOptions> _demoOptions;

    public TenantContextAccessor(IHttpContextAccessor httpContextAccessor, IConfiguration configuration, IOptions<DemoModeOptions> demoOptions)
    {
        _httpContextAccessor = httpContextAccessor;
        _configuration = configuration;
        _demoOptions = demoOptions;
    }

    public TenantContext Resolve()
    {
        var http = _httpContextAccessor.HttpContext;
        var claimValue = http?.User.FindFirstValue("tenant_id") ?? http?.User.FindFirstValue("tenantId");
        if (TryParsePositive(claimValue, out var claimTenant)) return new TenantContext(claimTenant, "Tenant autenticado", false, "Real", null);

        if (http?.Request.Headers.TryGetValue("X-Sigov-Tenant-Id", out var header) == true && TryParsePositive(header.ToString(), out var headerTenant))
        {
            return new TenantContext(headerTenant, "Tenant por header interno", false, "Real", null);
        }

        var configured = _configuration["Sigov:Tenant:DefaultTenantId"];
        if (TryParsePositive(configured, out var configuredTenant)) return new TenantContext(configuredTenant, "Tenant configurado", false, "Real", null);

        var host = http?.Request.Host.Host ?? string.Empty;
        var hostTenant = _configuration[$"Sigov:Tenant:Hosts:{host}"];
        if (TryParsePositive(hostTenant, out var hostTenantId)) return new TenantContext(hostTenantId, host, false, "Real", null);

        if (_demoOptions.Value.Enabled)
        {
            var demoTenant = _configuration.GetValue<long?>("Sigov:DemoMode:TenantId") ?? 1L;
            return new TenantContext(demoTenant, "Tenant demo", false, "Demo", "Tenant demo resolvido apenas porque Sigov:DemoMode:Enabled=true.");
        }

        return new TenantContext(null, "Admin Global", true, "Real", "Nenhum tenant específico resolvido; painel operando em modo Admin Global agregado.");
    }

    private static bool TryParsePositive(string? value, out long tenantId)
    {
        tenantId = 0;
        return !string.IsNullOrWhiteSpace(value) && long.TryParse(value, out tenantId) && tenantId > 0;
    }
}
