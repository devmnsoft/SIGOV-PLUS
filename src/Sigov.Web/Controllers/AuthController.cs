using System.Security.Claims;
using Dapper;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Sigov.Application.Abstractions;
using Sigov.Infrastructure.Persistence.Dapper;
using Sigov.Web.Models.Auth;
using Sigov.Web.Services;

namespace Sigov.Web.Controllers;

public sealed class AuthController : Controller
{
    private readonly NpgsqlConnectionFactory _connectionFactory;
    private readonly IPasswordHashService _passwordHashService;
    private readonly ILogger<AuthController> _logger;
    private readonly IAuditTrailService _auditTrail;

    public AuthController(NpgsqlConnectionFactory connectionFactory, IPasswordHashService passwordHashService, IAuditTrailService auditTrail, ILogger<AuthController> logger)
    {
        _connectionFactory = connectionFactory;
        _passwordHashService = passwordHashService;
        _auditTrail = auditTrail;
        _logger = logger;
    }

    [HttpGet]
    public IActionResult Login(string? returnUrl = null)
    {
        try
        {
            ViewData["ReturnUrl"] = returnUrl;
            return View(new LoginViewModel());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro tratado ao abrir login. CorrelationId={CorrelationId}", HttpContext.TraceIdentifier);
            return StatusCode(StatusCodes.Status500InternalServerError, "Não foi possível abrir o login.");
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel model, string? returnUrl = null, CancellationToken cancellationToken = default)
    {
        ViewData["ReturnUrl"] = returnUrl;
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
        var correlationId = HttpContext.TraceIdentifier;
        try
        {
            const string sql = @"select id, tenant_id as TenantId, coalesce(nome, login) as Nome, login, email, senha_hash as SenhaHash, ativo, bloqueado
from sigov.usuario
where is_deleted = false and (lower(login) = lower(@Login) or lower(email) = lower(@Login))
limit 1;";
            using var connection = _connectionFactory.CreateConnection();
            var user = await connection.QuerySingleOrDefaultAsync<LoginUserRow>(new CommandDefinition(sql, new { model.Login }, cancellationToken: cancellationToken)).ConfigureAwait(false);
            var valid = user is not null && user.Ativo && !user.Bloqueado && _passwordHashService.VerifyPassword(model.Senha, user.SenhaHash);
            await _auditTrail.RegistrarAsync(user?.TenantId, user?.Id, valid ? "LOGIN_SUCESSO" : "LOGIN_FALHA", "sigov.usuario", user?.Id.ToString(), null, new { login = model.Login }, ip, Request.Headers["User-Agent"].ToString(), correlationId, cancellationToken).ConfigureAwait(false);
            _logger.LogInformation("Tentativa de login SIGOV para {Login} em {Ip}: {Resultado}. CorrelationId={CorrelationId}", model.Login, ip, valid ? "sucesso" : "falha", correlationId);

            if (!valid || user is null)
            {
                model.MensagemErro = "Credenciais inválidas ou usuário bloqueado.";
                return View(model);
            }

            var claims = new List<Claim>
            {
                new(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new(ClaimTypes.Name, user.Nome),
                new(ClaimTypes.Email, user.Email),
                new("login", user.Login),
                new("tenant_id", user.TenantId?.ToString() ?? string.Empty),
                new(ClaimTypes.Role, "ADMINISTRADOR_GERAL")
            };
            await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme)), new AuthenticationProperties
            {
                IsPersistent = false,
                AllowRefresh = true,
                ExpiresUtc = DateTimeOffset.UtcNow.AddHours(8)
            }).ConfigureAwait(false);

            return LocalRedirect(string.IsNullOrWhiteSpace(returnUrl) || !Url.IsLocalUrl(returnUrl) ? "/MinhaCentral" : returnUrl);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro tratado ao autenticar {Login}. CorrelationId={CorrelationId}", model.Login, correlationId);
            model.MensagemErro = "Não foi possível autenticar agora. Tente novamente ou verifique o ambiente local.";
            return View(model);
        }
    }

    [HttpGet]
    public async Task<IActionResult> Logout(CancellationToken cancellationToken)
    {
        var login = User.Identity?.Name ?? "anonimo";
        try
        {
            await _auditTrail.RegistrarAsync(null, CurrentUserId(), "LOGOUT", "sigov.usuario", CurrentUserId()?.ToString(), null, new { login }, HttpContext.Connection.RemoteIpAddress?.ToString(), Request.Headers["User-Agent"].ToString(), HttpContext.TraceIdentifier, cancellationToken).ConfigureAwait(false);
            _logger.LogInformation("Logout SIGOV para {Login}. CorrelationId={CorrelationId}", login, HttpContext.TraceIdentifier);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falha ao auditar logout de {Login}.", login);
        }
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme).ConfigureAwait(false);
        return RedirectToAction(nameof(Login));
    }

    private long? CurrentUserId() => long.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;

    private static async Task RegistrarAuditoriaAsync(System.Data.IDbConnection connection, string acao, long? tenantId, long? usuarioId, string login, string? ip, string correlationId, CancellationToken cancellationToken)
    {
        const string sql = @"insert into sigov.auditoria_evento (tenant_id, usuario_id, acao, entidade, entidade_id, ip, user_agent, depois, correlation_id)
values (@TenantId, @UsuarioId, @Acao, 'sigov.usuario', @EntidadeId, @Ip, null, jsonb_build_object('login', @Login), @CorrelationId::uuid);";
        var correlation = Guid.TryParse(correlationId, out var parsed) ? parsed : Guid.NewGuid();
        await connection.ExecuteAsync(new CommandDefinition(sql, new { TenantId = tenantId, UsuarioId = usuarioId, Acao = acao, EntidadeId = usuarioId?.ToString(), Ip = ip, Login = login, CorrelationId = correlation }, cancellationToken: cancellationToken)).ConfigureAwait(false);
    }

    private sealed record LoginUserRow(long Id, long? TenantId, string Nome, string Login, string Email, string SenhaHash, bool Ativo, bool Bloqueado);
}
