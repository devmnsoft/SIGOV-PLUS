using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sigov.Api.Contracts;
using Sigov.Application.Saas.Context;

namespace Sigov.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/saas/contexto")]
public sealed class TenantContextController(IOperationalContextService service, ILogger<TenantContextController> logger) : ControllerBase
{
    private const string SessionCookie = "__Host-Sigov.Context";

    [HttpGet("atual")]
    public async Task<ActionResult<ApiResponse<OperationalContext?>>> Current(CancellationToken ct)
    {
        var sessionHash = RequiredSessionHash();
        return Ok(ApiResponse<OperationalContext?>.Ok(await service.CurrentAsync(UserId(), sessionHash, ct).ConfigureAwait(false)));
    }

    [HttpGet("sessao/resumo")]
    public async Task<ActionResult<ApiResponse<OperationalContext?>>> Session(CancellationToken ct)
    {
        var sessionHash = RequiredSessionHash();
        return Ok(ApiResponse<OperationalContext?>.Ok(await service.CurrentAsync(UserId(), sessionHash, ct).ConfigureAwait(false)));
    }

    [HttpGet("empresas")]
    public async Task<ActionResult<ApiResponse<IReadOnlyCollection<ContextOption>>>> Tenants([FromQuery] string? busca, [FromQuery] int pagina = 1, [FromQuery] int tamanho = 20, CancellationToken ct = default)
        => Ok(ApiResponse<IReadOnlyCollection<ContextOption>>.Ok(await service.SearchTenantsAsync(UserId(), busca, pagina, tamanho, ct).ConfigureAwait(false)));

    [HttpGet("empresas/{tenantId:long}/unidades")]
    public Task<ActionResult<ApiResponse<IReadOnlyCollection<ContextOption>>>> Units(long tenantId, CancellationToken ct) => Options(tenantId, ContextOptionType.Unidade, ct);

    [HttpGet("empresas/{tenantId:long}/exercicios")]
    public Task<ActionResult<ApiResponse<IReadOnlyCollection<ContextOption>>>> Exercises(long tenantId, CancellationToken ct) => Options(tenantId, ContextOptionType.Exercicio, ct);

    [HttpGet("empresas/{tenantId:long}/sistemas")]
    public Task<ActionResult<ApiResponse<IReadOnlyCollection<ContextOption>>>> Systems(long tenantId, CancellationToken ct) => Options(tenantId, ContextOptionType.Sistema, ct);

    [HttpPost("validar")]
    public async Task<ActionResult<ApiResponse<ContextValidation>>> Validate([FromBody] ContextSelection request, CancellationToken ct)
        => Ok(ApiResponse<ContextValidation>.Ok(await service.ValidateAsync(UserId(), request, ct).ConfigureAwait(false)));

    [HttpPost("selecionar")]
    public async Task<ActionResult<ApiResponse<OperationalContext>>> Select([FromBody] ContextSelection request, CancellationToken ct)
    {
        try
        {
            var result = await service.SelectAsync(Change(request), ct).ConfigureAwait(false);
            logger.LogInformation("Contexto alterado. UsuarioId={UsuarioId} SessaoId={SessaoId} TenantId={TenantId} SistemaId={SistemaId} CorrelationId={CorrelationId}", result.UsuarioId, result.SessionId, result.TenantId, result.SistemaId, HttpContext.TraceIdentifier);
            return Ok(ApiResponse<OperationalContext>.Ok(result));
        }
        catch (ArgumentException ex) { return BadRequest(ApiResponse<OperationalContext>.Fail(ex.Message)); }
        catch (InvalidOperationException ex) { logger.LogWarning("Troca de contexto negada. UsuarioId={UsuarioId} Codigo={Codigo} CorrelationId={CorrelationId}", UserId(), ex.Message.Split(':')[0], HttpContext.TraceIdentifier); return StatusCode(StatusCodes.Status403Forbidden, ApiResponse<OperationalContext>.Fail("Contexto não autorizado ou indisponível.")); }
    }

    [HttpPost("global")]
    public async Task<ActionResult<ApiResponse<OperationalContext>>> Global(CancellationToken ct)
        => Ok(ApiResponse<OperationalContext>.Ok(await service.ReturnGlobalAsync(Change(null), ct).ConfigureAwait(false)));

    [HttpDelete("sessao")]
    public async Task<IActionResult> End(CancellationToken ct)
    {
        var hash = SessionHash(create: false);
        if (hash is not null) await service.EndAsync(UserId(), hash, HttpContext.TraceIdentifier, RemoteIp(), Request.Headers.UserAgent.ToString(), ct).ConfigureAwait(false);
        Response.Cookies.Delete(SessionCookie);
        return NoContent();
    }

    private async Task<ActionResult<ApiResponse<IReadOnlyCollection<ContextOption>>>> Options(long tenantId, ContextOptionType type, CancellationToken ct)
        => Ok(ApiResponse<IReadOnlyCollection<ContextOption>>.Ok(await service.OptionsAsync(UserId(), tenantId, type, ct).ConfigureAwait(false)));

    private long UserId()
    {
        var raw = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("usuario_id") ?? User.FindFirstValue("sub");
        if (!long.TryParse(raw, out var id) || id <= 0) throw new UnauthorizedAccessException("Identidade autenticada inválida.");
        return id;
    }

    private ContextChange Change(ContextSelection? selection) => new(UserId(), RequiredSessionHash(), selection, HttpContext.TraceIdentifier, RemoteIp(), Request.Headers.UserAgent.ToString()[..Math.Min(Request.Headers.UserAgent.ToString().Length, 500)], DateTimeOffset.UtcNow.AddHours(8));
    private string? RemoteIp() => HttpContext.Connection.RemoteIpAddress?.ToString();

    private string RequiredSessionHash() => SessionHash(create: true)
        ?? throw new InvalidOperationException("Não foi possível estabelecer a sessão de contexto.");

    private string? SessionHash(bool create)
    {
        if (!Request.Cookies.TryGetValue(SessionCookie, out var token) || token.Length < 43)
        {
            if (!create) return null;
            token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)).TrimEnd('=').Replace('+', '-').Replace('/', '_');
            Response.Cookies.Append(SessionCookie, token, new CookieOptions { HttpOnly = true, Secure = true, SameSite = SameSiteMode.Lax, IsEssential = true, MaxAge = TimeSpan.FromHours(8), Path = "/" });
        }
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
    }
}
