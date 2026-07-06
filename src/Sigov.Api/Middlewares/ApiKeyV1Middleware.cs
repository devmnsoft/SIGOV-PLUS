using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
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

    public async Task InvokeAsync(HttpContext context, DapperContext db)
    {
        if (!context.Request.Path.StartsWithSegments("/api/v1", StringComparison.OrdinalIgnoreCase) || IsPublic(context.Request.Path))
        {
            await _next(context).ConfigureAwait(false);
            return;
        }

        var started = DateTimeOffset.UtcNow;
        var watch = Stopwatch.StartNew();
        long? tenantId = null;
        long? apiKeyId = null;
        var correlation = context.Items[CorrelationIdMiddleware.HeaderName]?.ToString() ?? context.TraceIdentifier;

        try
        {
            var apiKey = context.Request.Headers["X-Api-Key"].FirstOrDefault();
            if (string.IsNullOrWhiteSpace(apiKey))
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                await context.Response.WriteAsJsonAsync(new { error = "api_key_required", message = "Header X-Api-Key obrigatório." }, context.RequestAborted).ConfigureAwait(false);
                return;
            }

            if (!long.TryParse(context.Request.Headers["X-Tenant-Id"].FirstOrDefault(), out var parsedTenant) || parsedTenant <= 0)
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                await context.Response.WriteAsJsonAsync(new { error = "tenant_required", message = "Header X-Tenant-Id obrigatório para API v1." }, context.RequestAborted).ConfigureAwait(false);
                return;
            }
            tenantId = parsedTenant;

            var requiredScope = ResolveScope(context.Request.Path, context.Request.Method);
            using var cn = db.CreateConnection();
            var rows = await cn.QueryAsync<ApiKeyRow>(new CommandDefinition(@"
select ak.id as Id, ak.tenant_id as TenantId, ak.api_key_hash as ApiKeyHash, ak.status as Status,
       coalesce(array_agg(ake.escopo) filter (where ake.escopo is not null), array[]::text[]) as Scopes
  from sigov.api_key ak
  left join sigov.api_key_escopo ake on ake.api_key_id = ak.id and ake.tenant_id = ak.tenant_id and ake.is_deleted = false
 where ak.tenant_id = @TenantId and ak.is_deleted = false and ak.status = 'ATIVA'
 group by ak.id, ak.tenant_id, ak.api_key_hash, ak.status;", new { TenantId = tenantId }, cancellationToken: context.RequestAborted)).ConfigureAwait(false);
            var apiKeyHash = Hash(apiKey);
            var row = rows.FirstOrDefault(candidate => FixedEquals(apiKeyHash, candidate.ApiKeyHash));

            if (row is null)
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                await context.Response.WriteAsJsonAsync(new { error = "api_key_invalid", message = "API key ausente ou inválida." }, context.RequestAborted).ConfigureAwait(false);
                return;
            }

            apiKeyId = row.Id;
            if (!string.IsNullOrWhiteSpace(requiredScope) && !row.Scopes.Contains(requiredScope, StringComparer.OrdinalIgnoreCase))
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                await context.Response.WriteAsJsonAsync(new { error = "scope_denied", scope = requiredScope, message = "Escopo insuficiente para o endpoint." }, context.RequestAborted).ConfigureAwait(false);
                return;
            }

            context.Items["ApiKeyId"] = apiKeyId;
            await _next(context).ConfigureAwait(false);
        }
        finally
        {
            watch.Stop();
            await SafeLogAsync(db, tenantId, apiKeyId, context, correlation, started, watch.ElapsedMilliseconds).ConfigureAwait(false);
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
    private static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();
    private static bool FixedEquals(string a, string b)
    {
        var left = Encoding.UTF8.GetBytes(a);
        var right = Encoding.UTF8.GetBytes(b ?? string.Empty);
        return left.Length == right.Length && CryptographicOperations.FixedTimeEquals(left, right);
    }
    private static async Task SafeLogAsync(DapperContext db, long? tenantId, long? apiKeyId, HttpContext context, string correlation, DateTimeOffset started, long elapsedMs)
    {
        try
        {
            using var cn = db.CreateConnection();
            await cn.ExecuteAsync(new CommandDefinition(@"insert into sigov.api_requisicao_log (tenant_id, api_key_id, endpoint, method, status_code, correlation_id, ip, user_agent, started_at, elapsed_ms)
values (@TenantId, @ApiKeyId, @Endpoint, @Method, @StatusCode, cast(@CorrelationId as uuid), @Ip, @UserAgent, @StartedAt, @ElapsedMs);", new { TenantId = tenantId, ApiKeyId = apiKeyId, Endpoint = context.Request.Path.Value, Method = context.Request.Method, StatusCode = context.Response.StatusCode, CorrelationId = Guid.TryParse(correlation, out var g) ? g : Guid.NewGuid(), Ip = context.Connection.RemoteIpAddress?.ToString(), UserAgent = context.Request.Headers.UserAgent.ToString(), StartedAt = started, ElapsedMs = elapsedMs }, cancellationToken: context.RequestAborted)).ConfigureAwait(false);
        }
        catch { }
    }
    private sealed record ApiKeyRow(long Id, long TenantId, string ApiKeyHash, string Status, string[] Scopes);
}
