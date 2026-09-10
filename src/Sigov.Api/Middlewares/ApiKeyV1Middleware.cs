using System.Diagnostics;
using Dapper;
using Sigov.Infrastructure.Persistence.Dapper;

namespace Sigov.Api.Middlewares;

public sealed class ApiKeyV1Middleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ApiKeyV1Middleware> _logger;

    public ApiKeyV1Middleware(RequestDelegate next, ILogger<ApiKeyV1Middleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (!context.Request.Path.StartsWithSegments("/api/v1", StringComparison.OrdinalIgnoreCase) || IsPublic(context.Request.Path))
        {
            await _next(context).ConfigureAwait(false);
            return;
        }

        var db = context.RequestServices.GetRequiredService<DapperContext>();

        var started = DateTimeOffset.UtcNow;
        var watch = Stopwatch.StartNew();
        long? tenantId = null;
        long? apiKeyId = null;
        var correlation = context.Items[CorrelationIdMiddleware.HeaderName]?.ToString() ?? context.TraceIdentifier;

        try
        {
            if (!long.TryParse(context.Request.Headers["X-Tenant-Id"].FirstOrDefault(), out var parsedTenant) || parsedTenant <= 0)
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                await context.Response.WriteAsJsonAsync(new { error = "tenant_required", message = "Header X-Tenant-Id obrigatório para API v1." }, context.RequestAborted).ConfigureAwait(false);
                return;
            }
            tenantId = parsedTenant;

            if (context.User.Identity?.IsAuthenticated != true)
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                await context.Response.WriteAsJsonAsync(new { error = "api_key_required", message = "Header X-Api-Key obrigatório e válido." }, context.RequestAborted).ConfigureAwait(false);
                return;
            }

            if (!long.TryParse(context.User.FindFirst("api_key_id")?.Value, out var parsedApiKeyId) || parsedApiKeyId <= 0)
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                await context.Response.WriteAsJsonAsync(new { error = "api_key_invalid", message = "API key ausente ou inválida." }, context.RequestAborted).ConfigureAwait(false);
                return;
            }
            apiKeyId = parsedApiKeyId;

            if (!long.TryParse(context.User.FindFirst("tenant_id")?.Value, out var authenticatedTenant) || authenticatedTenant != tenantId)
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                await context.Response.WriteAsJsonAsync(new { error = "tenant_mismatch", message = "API key não pertence ao tenant informado." }, context.RequestAborted).ConfigureAwait(false);
                return;
            }

            var requiredScope = ResolveScope(context.Request.Path, context.Request.Method);
            var scopes = context.User.FindAll("scope").Select(claim => claim.Value);
            if (!string.IsNullOrWhiteSpace(requiredScope) && !scopes.Contains(requiredScope, StringComparer.OrdinalIgnoreCase))
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                await context.Response.WriteAsJsonAsync(new { error = "scope_denied", scope = requiredScope, message = "Escopo insuficiente para o endpoint." }, context.RequestAborted).ConfigureAwait(false);
                return;
            }

            await _next(context).ConfigureAwait(false);
        }
        finally
        {
            watch.Stop();
            await SafeLogAsync(db, tenantId, apiKeyId, context, correlation, started, watch.ElapsedMilliseconds, _logger).ConfigureAwait(false);
        }
    }

    private static bool IsPublic(PathString path) => path.StartsWithSegments("/api/v1/health", StringComparison.OrdinalIgnoreCase) || path.StartsWithSegments("/api/health", StringComparison.OrdinalIgnoreCase);
    private static string ResolveScope(PathString path, string method)
    {
        var write = HttpMethods.IsPost(method) || HttpMethods.IsPut(method) || HttpMethods.IsPatch(method) || HttpMethods.IsDelete(method);
        var p = path.Value ?? string.Empty;
        if (p.Contains("/protocolos", StringComparison.OrdinalIgnoreCase)) return write ? "protocolos.write" : "protocolos.read";
        if (p.Contains("/documentos", StringComparison.OrdinalIgnoreCase)) return write ? "documentos.write" : "documentos.read";
        if (p.Contains("/tarefas", StringComparison.OrdinalIgnoreCase)) return write ? "tarefas.write" : "tarefas.read";
        if (p.Contains("/notificacoes", StringComparison.OrdinalIgnoreCase)) return "notificacoes.read";
        if (p.Contains("/webhooks", StringComparison.OrdinalIgnoreCase)) return "webhooks.manage";
        if (p.Contains("/mobile", StringComparison.OrdinalIgnoreCase)) return "mobile.sync";
        if (p.Contains("/assinaturas", StringComparison.OrdinalIgnoreCase)) return write ? "assinaturas.write" : "assinaturas.read";
        if (p.Contains("/bi", StringComparison.OrdinalIgnoreCase)) return "bi.read";
        return string.Empty;
    }
    private static async Task SafeLogAsync(DapperContext db, long? tenantId, long? apiKeyId, HttpContext context, string correlation, DateTimeOffset started, long elapsedMs, ILogger logger)
    {
        try
        {
            using var cn = db.CreateConnection();
            await cn.ExecuteAsync(new CommandDefinition(@"insert into sigov.api_requisicao_log (tenant_id, api_key_id, endpoint, method, status_code, correlation_id, ip, user_agent, started_at, elapsed_ms)
values (@TenantId, @ApiKeyId, @Endpoint, @Method, @StatusCode, cast(@CorrelationId as uuid), @Ip, @UserAgent, @StartedAt, @ElapsedMs);", new { TenantId = tenantId, ApiKeyId = apiKeyId, Endpoint = context.Request.Path.Value, Method = context.Request.Method, StatusCode = context.Response.StatusCode, CorrelationId = Guid.TryParse(correlation, out var g) ? g : Guid.NewGuid(), Ip = context.Connection.RemoteIpAddress?.ToString(), UserAgent = context.Request.Headers.UserAgent.ToString(), StartedAt = started, ElapsedMs = elapsedMs }, cancellationToken: context.RequestAborted)).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Falha ao persistir auditoria da API. TenantId={TenantId}; ApiKeyId={ApiKeyId}; CorrelationId={CorrelationId}", tenantId, apiKeyId, correlation);
        }
    }
}
