using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Npgsql;
using Sigov.Application.Abstractions;
using Sigov.Application.Security;
using Sigov.Web.Models.Auth;
using Sigov.Web.Services;

namespace Sigov.Web.Controllers;

public sealed class AuthController : Controller
{
    private readonly IAuthenticationRepository _authenticationRepository;
    private readonly IPasswordHashService _passwordHashService;
    private readonly IPasswordPolicyService _passwordPolicy;
    private readonly ICurrentUser _currentUser;
    private readonly ILogger<AuthController> _logger;
    private readonly IAuditTrailService _auditTrail;
    private readonly IPasswordRecoveryService _passwordRecoveryService;
    private readonly IConfiguration _configuration;
    private readonly IWebHostEnvironment _environment;

    public AuthController(IAuthenticationRepository authenticationRepository, IPasswordHashService passwordHashService, IPasswordPolicyService passwordPolicy, ICurrentUser currentUser, IAuditTrailService auditTrail, IPasswordRecoveryService passwordRecoveryService, IConfiguration configuration, IWebHostEnvironment environment, ILogger<AuthController> logger)
    {
        _authenticationRepository = authenticationRepository;
        _passwordHashService = passwordHashService;
        _passwordPolicy = passwordPolicy;
        _currentUser = currentUser;
        _auditTrail = auditTrail;
        _passwordRecoveryService = passwordRecoveryService;
        _configuration = configuration;
        _environment = environment;
        _logger = logger;
    }

    [HttpGet]
    [AllowAnonymous]
    public IActionResult Login(string? returnUrl = null)
    {
        PrepareLoginView(returnUrl);
        return View(new LoginViewModel());
    }

    [HttpPost]
    [AllowAnonymous]
    [EnableRateLimiting("authentication")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel model, string? returnUrl = null, CancellationToken cancellationToken = default)
    {
        PrepareLoginView(returnUrl);
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
        var correlationId = HttpContext.TraceIdentifier;
        try
        {
            var login = model.Login ?? string.Empty;
            var senha = model.Senha ?? string.Empty;
            var user = await _authenticationRepository.FindForLoginAsync(login, cancellationToken).ConfigureAwait(false);
            var valid = user is not null && user.Ativo && !user.Bloqueado && _passwordHashService.VerifyPassword(senha, user.PasswordHash);
            await _auditTrail.RegistrarAsync(user?.TenantId, user?.Id, valid ? "LOGIN_SUCESSO" : "LOGIN_FALHA", "sigov.usuario", user?.Id.ToString(), null, new { login = model.Login }, ip, Request.Headers["User-Agent"].ToString(), correlationId, cancellationToken).ConfigureAwait(false);
            _logger.LogInformation("Tentativa de login SIGOV: {Resultado}. CorrelationId={CorrelationId}", valid ? "sucesso" : "falha", correlationId);

            if (!valid || user is null)
            {
                model.MensagemErro = "Credenciais inválidas ou usuário bloqueado.";
                return View(model);
            }

            var access = await _authenticationRepository.GetAccessAsync(user.Id, cancellationToken).ConfigureAwait(false);
            var claims = new List<Claim>
            {
                new(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new(ClaimTypes.Name, user.Nome),
                new(ClaimTypes.Email, user.Email),
                new("login", user.Login),
                new("tenant_id", user.TenantId?.ToString() ?? string.Empty)
            };
            if (!string.IsNullOrWhiteSpace(user.TenantName)) claims.Add(new Claim("tenant_name", user.TenantName));
            if (user.DeveAlterarSenha) claims.Add(new Claim("password_change_required", "true"));
            claims.AddRange(access.Roles.Select(role => new Claim(ClaimTypes.Role, role)));
            claims.AddRange(access.Permissions.Select(permission => new Claim("permission", permission)));
            await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme)), new AuthenticationProperties
            {
                IsPersistent = model.LembrarLogin,
                AllowRefresh = true,
                ExpiresUtc = DateTimeOffset.UtcNow.AddHours(8)
            }).ConfigureAwait(false);

            if (user.DeveAlterarSenha) return RedirectToAction(nameof(TrocarSenhaInicial));
            return LocalRedirect(string.IsNullOrWhiteSpace(returnUrl) || !Url.IsLocalUrl(returnUrl) ? "/MinhaCentral" : returnUrl);
        }
        catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.InvalidCatalogName)
        {
            _logger.LogError("Banco PostgreSQL configurado não existe. Execute ./scripts/setup-dev.ps1. CorrelationId={CorrelationId}", correlationId);
            model.MensagemErro = _environment.IsDevelopment()
                ? "Banco de dados local não encontrado. Execute o provisionamento do ambiente."
                : "Não foi possível autenticar agora. Tente novamente mais tarde.";
            return View(model);
        }
        catch (NpgsqlException ex)
        {
            _logger.LogError(ex, "Banco indisponível durante autenticação. Execute ./scripts/setup-dev.ps1 no ambiente local. CorrelationId={CorrelationId}", correlationId);
            model.MensagemErro = _environment.IsDevelopment()
                ? "Banco de dados local indisponível. Execute o provisionamento do ambiente."
                : "Não foi possível autenticar agora. Tente novamente mais tarde.";
            return View(model);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro tratado ao autenticar. CorrelationId={CorrelationId}", correlationId);
            model.MensagemErro = "Não foi possível autenticar agora. Tente novamente ou verifique o ambiente local.";
            return View(model);
        }
    }

    private void PrepareLoginView(string? returnUrl)
    {
        ViewData["ReturnUrl"] = returnUrl;
        ViewData["ShowDevelopmentHelp"] = _environment.IsDevelopment();
        ViewData["SwaggerUrl"] = _configuration["Sigov:SwaggerUrl"] ?? "https://localhost:7001/swagger";
    }



    [HttpGet("Auth/EsqueciSenha")]
    [HttpGet("Auth/EsqueciMinhaSenha")]
    [AllowAnonymous]
    public IActionResult EsqueciMinhaSenha()
    {
        return View(new ForgotPasswordViewModel());
    }

    [HttpPost("Auth/EsqueciSenha")]
    [HttpPost("Auth/EsqueciMinhaSenha")]
    [AllowAnonymous]
    [EnableRateLimiting("password-recovery")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EsqueciMinhaSenha(ForgotPasswordViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        try
        {
            var result = await _passwordRecoveryService.RequestAsync(model.LoginOuEmail ?? string.Empty, BuildPasswordResetUrl, cancellationToken).ConfigureAwait(false);
            await _auditTrail.RegistrarAsync(null, null, "RECUPERACAO_SENHA_SOLICITADA", "sigov.usuario", null, null, new { canal = "web", informado = true }, HttpContext.Connection.RemoteIpAddress?.ToString(), Request.Headers["User-Agent"].ToString(), HttpContext.TraceIdentifier, cancellationToken).ConfigureAwait(false);
            if (result == PasswordRecoveryResult.Sent)
                await _auditTrail.RegistrarAsync(null, null, "RECUPERACAO_SENHA_ENVIADA", "sigov.usuario", null, null, null, null, null, HttpContext.TraceIdentifier, cancellationToken).ConfigureAwait(false);
            _logger.LogInformation("Solicitação de recuperação de senha registrada. CorrelationId={CorrelationId}", HttpContext.TraceIdentifier);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falha no processamento da recuperação de senha. CorrelationId={CorrelationId}", HttpContext.TraceIdentifier);
            try
            {
                await _auditTrail.RegistrarAsync(null, null, "RECUPERACAO_SENHA_FALHA_ENVIO", "sigov.usuario", null, null, null, null, null, HttpContext.TraceIdentifier, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception auditException)
            {
                _logger.LogError(auditException, "Falha adicional ao auditar indisponibilidade da recuperação. CorrelationId={CorrelationId}", HttpContext.TraceIdentifier);
            }
        }

        model.Solicitado = true;
        model.Mensagem = "Se os dados informados corresponderem a uma conta ativa, as instruções serão enviadas pelo canal configurado pelo administrador.";
        return View(model);
    }

    [HttpGet("Auth/SolicitacaoEnviada")]
    public IActionResult SolicitacaoEnviada() => View();

    [Authorize]
    [HttpGet("Auth/TrocarSenhaInicial")]
    public IActionResult TrocarSenhaInicial() => View("AlterarSenha", new ChangePasswordViewModel());

    [Authorize]
    [HttpPost("Auth/TrocarSenhaInicial")]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> TrocarSenhaInicial(ChangePasswordViewModel model, CancellationToken ct) => AlterarSenhaCore(model, ct);

    [Authorize]
    [HttpGet("Auth/AlterarSenha")]
    public IActionResult AlterarSenha() => View(new ChangePasswordViewModel());

    [Authorize]
    [HttpPost("Auth/AlterarSenha")]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> AlterarSenha(ChangePasswordViewModel model, CancellationToken ct) => AlterarSenhaCore(model, ct);

    [HttpGet("Auth/RedefinirSenha")]
    [AllowAnonymous]
    public IActionResult RedefinirSenha(string token)
    {
        var model = new ResetPasswordViewModel { Token = token ?? string.Empty };
        if (!IsWellFormedToken(model.Token))
            ModelState.AddModelError(string.Empty, "O link de redefinição é inválido ou expirou. Solicite uma nova recuperação de senha.");
        return View(model);
    }

    [HttpPost("Auth/RedefinirSenha")]
    [AllowAnonymous]
    [EnableRateLimiting("password-recovery")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RedefinirSenha(ResetPasswordViewModel model, CancellationToken ct)
    {
        var novaSenha = model.NovaSenha ?? string.Empty;
        var confirmacao = model.Confirmacao ?? string.Empty;
        var tokenValue = model.Token ?? string.Empty;
        ValidatePassword(novaSenha, confirmacao);
        if (!IsWellFormedToken(tokenValue))
            ModelState.AddModelError(string.Empty, "O link de redefinição é inválido ou expirou. Solicite uma nova recuperação de senha.");
        if (!ModelState.IsValid) return View(model);
        var hash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(tokenValue)));
        var passwordHash = _passwordHashService.HashPassword(novaSenha);
        var changed = await _authenticationRepository.ConsumePasswordResetTokenAsync(hash, passwordHash, ct).ConfigureAwait(false);
        if (changed is null) { ModelState.AddModelError(string.Empty, "O link de redefinição é inválido ou expirou. Solicite uma nova recuperação de senha."); return View(model); }
        await _auditTrail.RegistrarAsync(changed.TenantId, changed.Id, "SENHA_REDEFINIDA", "sigov.usuario", changed.Id.ToString(), null, new { origem = "recuperacao" }, null, null, HttpContext.TraceIdentifier, ct).ConfigureAwait(false);
        return RedirectToAction(nameof(Login));
    }

    private async Task<IActionResult> AlterarSenhaCore(ChangePasswordViewModel model, CancellationToken ct)
    {
        var senhaAtual = model.SenhaAtual ?? string.Empty;
        var novaSenha = model.NovaSenha ?? string.Empty;
        var confirmacao = model.Confirmacao ?? string.Empty;
        ValidatePassword(novaSenha, confirmacao);
        if (!ModelState.IsValid) return View("AlterarSenha", model);
        var id = _currentUser.UserId;
        var tenantId = _currentUser.TenantId;
        if (id is null || tenantId is null) return Challenge();
        var currentHash = await _authenticationRepository.GetCurrentPasswordHashAsync(tenantId.Value, id.Value, ct).ConfigureAwait(false);
        if (string.IsNullOrEmpty(currentHash) || !_passwordHashService.VerifyPassword(senhaAtual, currentHash)) { ModelState.AddModelError(nameof(model.SenhaAtual), "Senha atual inválida."); return View("AlterarSenha", model); }
        if (_passwordHashService.VerifyPassword(novaSenha, currentHash)) { ModelState.AddModelError(nameof(model.NovaSenha), "A nova senha deve ser diferente da atual."); return View("AlterarSenha", model); }
        var changed = await _authenticationRepository.ChangePasswordAsync(tenantId.Value, id.Value, _passwordHashService.HashPassword(novaSenha), ct).ConfigureAwait(false);
        if (!changed) return NotFound();
        await _auditTrail.RegistrarAsync(tenantId, id, "SENHA_ALTERADA", "sigov.usuario", id.ToString(), null, new { origem = "usuario" }, null, null, HttpContext.TraceIdentifier, ct).ConfigureAwait(false);
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme).ConfigureAwait(false);
        TempData["Success"] = "Senha alterada. Entre novamente.";
        return RedirectToAction(nameof(Login));
    }

    private void ValidatePassword(string password, string confirmation)
    {
        foreach (var error in _passwordPolicy.Validate(password, confirmation))
            ModelState.AddModelError(error.Field, error.Message);
    }

    private string BuildPasswordResetUrl(string token)
    {
        var configuredBaseUrl = _configuration["PasswordRecovery:PublicBaseUrl"];
        var relative = Url.Action(nameof(RedefinirSenha), "Auth", new { token })
            ?? throw new InvalidOperationException("Não foi possível gerar a rota de redefinição de senha.");
        if (!string.IsNullOrWhiteSpace(configuredBaseUrl)) return new Uri(new Uri(configuredBaseUrl.TrimEnd('/') + "/"), relative.TrimStart('/')).AbsoluteUri;
        return $"{Request.Scheme}://{Request.Host}{Request.PathBase}{relative}";
    }

    private static bool IsWellFormedToken(string token) => token.Length == 43 && token.All(character => char.IsLetterOrDigit(character) || character is '-' or '_');

    [Authorize]
    [HttpPost("Auth/Logout")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout(CancellationToken cancellationToken)
    {
        var login = User.Identity?.Name ?? "anonimo";
        try
        {
            await _auditTrail.RegistrarAsync(_currentUser.TenantId, _currentUser.UserId, "LOGOUT", "sigov.usuario", _currentUser.UserId?.ToString(), null, new { login }, HttpContext.Connection.RemoteIpAddress?.ToString(), Request.Headers["User-Agent"].ToString(), HttpContext.TraceIdentifier, cancellationToken).ConfigureAwait(false);
            _logger.LogInformation("Logout SIGOV para {Login}. CorrelationId={CorrelationId}", login, HttpContext.TraceIdentifier);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falha ao auditar logout de {Login}.", login);
        }
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme).ConfigureAwait(false);
        return RedirectToAction(nameof(Login));
    }
}
