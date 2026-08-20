using System.Security.Claims;

namespace Sigov.Web.Services;

public interface ITenantContextAccessor { TenantContext Resolve(); }
public sealed record TenantContext(long? TenantId, string TenantNome, bool IsGlobal, string DataSource, string? MensagemFallback);

/// <summary>Compatibilidade para fluxos web legados. Nunca aceita tenant de header, query, configuração ou modo demo.</summary>
public sealed class TenantContextAccessor(IHttpContextAccessor httpContextAccessor) : ITenantContextAccessor
{
    public TenantContext Resolve()
    {
        var http = httpContextAccessor.HttpContext;
        if (http?.User.Identity?.IsAuthenticated != true)
            throw new UnauthorizedAccessException("Usuário autenticado é obrigatório para resolver o contexto.");
        var value = http.User.FindFirstValue("contexto_tenant_id");
        if (long.TryParse(value, out var tenantId) && tenantId > 0)
            return new TenantContext(tenantId, "Contexto autenticado", false, "Servidor", null);
        return new TenantContext(null, "Contexto global", true, "Servidor", "Selecione um contexto operacional antes de acessar módulos da empresa.");
    }
}
