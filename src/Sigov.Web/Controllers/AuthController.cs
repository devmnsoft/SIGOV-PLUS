using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
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
    private readonly ILogger<AuthController> _logger;
    private readonly IAuditTrailService _auditTrail;

    public AuthController(IAuthenticationRepository authenticationRepository, IPasswordHashService passwordHashService, IPasswordPolicyService passwordPolicy, IAuditTrailService auditTrail, ILogger<AuthController> logger)
    {
        _authenticationRepository = authenticationRepository;
        _passwordHashService = passwordHashService;
        _passwordPolicy = passwordPolicy;
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
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro tratado ao autenticar. CorrelationId={CorrelationId}", correlationId);
            model.MensagemErro = "Não foi possível autenticar agora. Tente novamente ou verifique o ambiente local.";
            return View(model);
        }
    }



    [HttpGet("Auth/EsqueciSenha")]
    [HttpGet]
    public IActionResult EsqueciMinhaSenha()
    {
        return View(new ForgotPasswordViewModel());
    }

    [HttpPost("Auth/EsqueciSenha")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EsqueciMinhaSenha(ForgotPasswordViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        try
        {
            var loginOuEmail = model.LoginOuEmail ?? string.Empty;
            var account = await _authenticationRepository.FindActiveAccountAsync(loginOuEmail, cancellationToken).ConfigureAwait(false);
            if (account is not null)
            {
                var token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)).Replace('+', '-').Replace('/', '_').TrimEnd('=');
                var tokenHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
                var correlationId = Guid.TryParse(HttpContext.TraceIdentifier, out var parsedCorrelationId) ? parsedCorrelationId : Guid.NewGuid();
                await _authenticationRepository.StorePasswordResetTokenAsync(account, tokenHash, correlationId, cancellationToken).ConfigureAwait(false);
                // O token somente deve ser entregue por um provedor transacional; nunca é incluído em logs ou na resposta.
            }
            await _auditTrail.RegistrarAsync(null, null, "RECUPERACAO_SENHA_SOLICITADA", "sigov.usuario", null, null, new { canal = "web", informado = true }, HttpContext.Connection.RemoteIpAddress?.ToString(), Request.Headers["User-Agent"].ToString(), HttpContext.TraceIdentifier, cancellationToken).ConfigureAwait(false);
            _logger.LogInformation("Solicitação de recuperação de senha registrada. CorrelationId={CorrelationId}", HttpContext.TraceIdentifier);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Falha ao auditar recuperação de senha. CorrelationId={CorrelationId}", HttpContext.TraceIdentifier);
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
    public IActionResult RedefinirSenha(string token) => View(new ResetPasswordViewModel { Token = token ?? string.Empty });

    [HttpPost("Auth/RedefinirSenha")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RedefinirSenha(ResetPasswordViewModel model, CancellationToken ct)
    {
        var novaSenha = model.NovaSenha ?? string.Empty;
        var confirmacao = model.Confirmacao ?? string.Empty;
        var tokenValue = model.Token ?? string.Empty;
        ValidatePassword(novaSenha, confirmacao);
        if (string.IsNullOrWhiteSpace(tokenValue))
            ModelState.AddModelError(nameof(model.Token), "Token de recuperação inválido.");
        if (!ModelState.IsValid) return View(model);
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(tokenValue)));
        var passwordHash = _passwordHashService.HashPassword(novaSenha);
        var changed = await _authenticationRepository.ConsumePasswordResetTokenAsync(hash, passwordHash, ct).ConfigureAwait(false);
        if (changed is null) { ModelState.AddModelError(string.Empty, "Link inválido ou expirado."); return View(model); }
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
        var id = CurrentUserId();
        var tenantId = CurrentTenantId();
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

    [HttpGet("Auth/Logout")]
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
    private long? CurrentTenantId() => long.TryParse(User.FindFirstValue("tenant_id"), out var id) && id > 0 ? id : null;
}
