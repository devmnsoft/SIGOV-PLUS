using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;
using Npgsql;
using Sigov.Application.Abstractions;
using Sigov.Application.Security;
using Sigov.Web.Models.Auth;
using Sigov.Web.Services;
using Sigov.Infrastructure.Diagnostics;

namespace Sigov.Web.Controllers;

public sealed class AuthController : Controller
{
    private readonly IAuthenticationRepository _authenticationRepository;
    private readonly IPasswordHashService _passwordHashService;
    private readonly IPasswordPolicyService _passwordPolicy;
    private readonly IIdentitySessionService _identitySessionService;
    private readonly ICurrentUser _currentUser;
    private readonly ILogger<AuthController> _logger;
    private readonly IAuditTrailService _auditTrail;
    private readonly IPasswordRecoveryService _passwordRecoveryService;
    private readonly IConfiguration _configuration;
    private readonly IWebHostEnvironment _environment;
    private readonly IOptionsMonitor<CookieAuthenticationOptions> _cookieOptions;

    public AuthController(IAuthenticationRepository authenticationRepository, IPasswordHashService passwordHashService, IPasswordPolicyService passwordPolicy, IIdentitySessionService identitySessionService, ICurrentUser currentUser, IAuditTrailService auditTrail, IPasswordRecoveryService passwordRecoveryService, IConfiguration configuration, IWebHostEnvironment environment, ILogger<AuthController> logger, IOptionsMonitor<CookieAuthenticationOptions> cookieOptions)
    {
        _authenticationRepository = authenticationRepository;
        _passwordHashService = passwordHashService;
        _passwordPolicy = passwordPolicy;
        _identitySessionService = identitySessionService;
        _currentUser = currentUser;
        _auditTrail = auditTrail;
        _passwordRecoveryService = passwordRecoveryService;
        _configuration = configuration;
        _environment = environment;
        _logger = logger;
        _cookieOptions = cookieOptions;
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
        if (_environment.IsDevelopment())
            _logger.LogInformation("Model binding de login Development. PasswordProvided={PasswordProvided}; PasswordLength={PasswordLength}; CorrelationId={CorrelationId}", !string.IsNullOrEmpty(model.Senha), model.Senha?.Length ?? 0, HttpContext.TraceIdentifier);
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
        var correlationId = HttpContext.TraceIdentifier;
        try
        {
            var identifier = AuthenticationIdentifierNormalizer.Normalize(model.Login);
            var senha = model.Senha ?? string.Empty;
            var candidates = identifier.IsValid
                ? await _authenticationRepository.FindLoginCandidatesAsync(identifier, cancellationToken).ConfigureAwait(false)
                : Array.Empty<AuthenticationUser>();
            var authenticated = new List<(AuthenticationUser User, AuthenticationAccess Access)>();
            foreach (var candidate in candidates)
            {
                if (!IsSupportedPasswordHash(candidate.PasswordHash) ||
                    !_passwordHashService.VerifyPassword(senha, candidate.PasswordHash) ||
                    !IsActive(candidate)) continue;

                var candidateAccess = await _authenticationRepository.GetAccessAsync(candidate.Id, cancellationToken).ConfigureAwait(false);
                if (candidateAccess.Roles.Count > 0 && candidateAccess.Permissions.Count > 0)
                    authenticated.Add((candidate, candidateAccess));
            }

            var tenantOptions = authenticated
                .Where(item => item.User.TenantId.HasValue)
                .GroupBy(item => item.User.TenantId!.Value)
                .Select(group => new LoginTenantOption(group.Key, group.First().User.TenantName))
                .OrderBy(option => option.Nome, StringComparer.CurrentCultureIgnoreCase)
                .ToArray();
            if (!model.TenantId.HasValue && tenantOptions.Length > 1)
            {
                model.Organizacoes = tenantOptions;
                model.Senha = string.Empty;
                model.MensagemErro = "Credenciais validadas. Selecione a organização e confirme a senha para continuar.";
                return View(model);
            }

            if (model.TenantId.HasValue)
                authenticated = authenticated.Where(item => item.User.TenantId == model.TenantId).ToList();

            var valid = authenticated.Count == 1;
            var user = valid ? authenticated[0].User : null;
            var access = valid ? authenticated[0].Access : new AuthenticationAccess(Array.Empty<string>(), Array.Empty<string>());
            if (_environment.IsDevelopment() && !valid)
            {
                _logger.LogWarning("Falha de login Development. Reason=INVALID_OR_AMBIGUOUS_CREDENTIAL; IdentifierKind={IdentifierKind}; CandidateCount={CandidateCount}; CorrelationId={CorrelationId}", identifier.Kind, candidates.Count, correlationId);
            }
            await _auditTrail.RegistrarAsync(user?.TenantId, user?.Id, valid ? "LOGIN_SUCESSO" : "LOGIN_FALHA", "sigov.usuario", user?.Id.ToString(), null,
                new { identificador_tipo = identifier.Kind.ToString(), identificador_hash = HashIdentifier(identifier.Value) },
                ip, Request.Headers["User-Agent"].ToString(), correlationId, cancellationToken).ConfigureAwait(false);
            _logger.LogInformation("Tentativa de login SIGOV: {Resultado}. CorrelationId={CorrelationId}", valid ? "sucesso" : "falha", correlationId);

            if (!valid || user is null)
            {
                model.MensagemErro = "Credenciais inválidas ou usuário bloqueado.";
                return View(model);
            }

            var cookieLifetime = TimeSpan.FromHours(_configuration.GetValue("Authentication:CookieHours", 8));
            var session = await _identitySessionService.CreateAsync(
                user,
                cookieLifetime,
                ip,
                Request.Headers["User-Agent"].ToString(),
                correlationId,
                cancellationToken).ConfigureAwait(false);

            var claims = new List<Claim>
            {
                new(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new(ClaimTypes.Name, user.Nome),
                new(ClaimTypes.Email, user.Email),
                new("login", user.Login),
                new("tenant_id", user.TenantId?.ToString() ?? string.Empty),
                new("entidade_id", user.EntidadeId?.ToString() ?? string.Empty),
                new("auth_version", session.AuthVersion.ToString(System.Globalization.CultureInfo.InvariantCulture)),
                new("session_id", session.SessionId.ToString(System.Globalization.CultureInfo.InvariantCulture)),
                new("session_token", session.Token)
            };
            if (!string.IsNullOrWhiteSpace(user.TenantName)) claims.Add(new Claim("tenant_name", user.TenantName));
            if (user.DeveAlterarSenha) claims.Add(new Claim("password_change_required", "true"));
            claims.AddRange(access.Roles.Select(role => new Claim(ClaimTypes.Role, role)));
            var properties = new AuthenticationProperties
            {
                IsPersistent = model.LembrarLogin,
                AllowRefresh = true,
                ExpiresUtc = session.ExpiresAt
            };
            var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme));
            var ticket = new AuthenticationTicket(principal, properties, CookieAuthenticationDefaults.AuthenticationScheme);
            var protectedTicketSize = System.Text.Encoding.UTF8.GetByteCount(
                _cookieOptions.Get(CookieAuthenticationDefaults.AuthenticationScheme).TicketDataFormat.Protect(ticket));
            _logger.LogInformation(
                "Ticket de autenticação compacto. Roles={RoleCount}; PermissionsLoaded={PermissionCount}; ProtectedTicketBytes={ProtectedTicketBytes}; EstimatedChunks={EstimatedChunks}; CorrelationId={CorrelationId}",
                access.Roles.Count, access.Permissions.Count, protectedTicketSize, Math.Max(1, (int)Math.Ceiling(protectedTicketSize / 4050d)), correlationId);
            DeleteLegacyAuthenticationChunks();
            await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal, properties).ConfigureAwait(false);

            if (user.DeveAlterarSenha) return RedirectToAction(nameof(TrocarSenhaInicial));
            return LocalRedirect(string.IsNullOrWhiteSpace(returnUrl) || !Url.IsLocalUrl(returnUrl) ? "/MinhaCentral" : returnUrl);
        }
        catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.InvalidCatalogName)
        {
            var target = SafeConnectionStringDiagnostics.Read(_configuration, _environment);
            _logger.LogError("O Web está tentando conectar em Host={Host} Port={Port} Database={Database} User={Username}. O banco não existe nessa instância. Environment={Environment}; CorrelationId={CorrelationId}", target.Host, target.Port, target.Database, target.Username, target.Environment, correlationId);
            model.MensagemErro = _environment.IsDevelopment()
                ? $"Banco local não encontrado em {target.Endpoint}. Execute: pwsh ./scripts/setup-dev.ps1 ou verifique: pwsh ./scripts/check-local-db.ps1"
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

    private static bool IsSupportedPasswordHash(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return false;
        var parts = value.Split('$');
        return parts.Length == 4 && parts[0] == "SIGOV_PBKDF2_V1" &&
               int.TryParse(parts[1], out var iterations) && iterations is >= 100000 and <= 1000000;
    }

    private static bool IsActive(AuthenticationUser user) => user.Ativo && !user.Bloqueado && !user.IsDeleted &&
        user.TenantAtivo && !user.TenantIsDeleted;

    private static string HashIdentifier(string value) => Convert.ToHexString(
        System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(value)));

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
        await _identitySessionService.RevokeAllForUserAsync(id.Value, "PASSWORD_CHANGED", ct).ConfigureAwait(false);
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
            if (_currentUser.UserId is long userId && long.TryParse(User.FindFirst("session_id")?.Value, out var sessionId))
                await _identitySessionService.RevokeAsync(sessionId, userId, "LOGOUT", cancellationToken).ConfigureAwait(false);
            await _auditTrail.RegistrarAsync(_currentUser.TenantId, _currentUser.UserId, "LOGOUT", "sigov.usuario", _currentUser.UserId?.ToString(), null, new { login }, HttpContext.Connection.RemoteIpAddress?.ToString(), Request.Headers["User-Agent"].ToString(), HttpContext.TraceIdentifier, cancellationToken).ConfigureAwait(false);
            _logger.LogInformation("Logout SIGOV para {Login}. CorrelationId={CorrelationId}", login, HttpContext.TraceIdentifier);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falha ao auditar logout de {Login}.", login);
        }
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme).ConfigureAwait(false);
        DeleteLegacyAuthenticationChunks(includeBaseCookie: true);
        return RedirectToAction(nameof(Login));
    }

    private void DeleteLegacyAuthenticationChunks(bool includeBaseCookie = false)
    {
        const string cookieName = "SIGOV.AUTH";
        const int maximumChunksToDelete = 64;
        var options = _cookieOptions.Get(CookieAuthenticationDefaults.AuthenticationScheme).Cookie.Build(HttpContext);
        var deleted = 0;
        foreach (var key in Request.Cookies.Keys)
        {
            var isBase = string.Equals(key, cookieName, StringComparison.Ordinal);
            var suffix = key.StartsWith(cookieName + "C", StringComparison.Ordinal) ? key[(cookieName.Length + 1)..] : string.Empty;
            if ((!includeBaseCookie || !isBase) && (!int.TryParse(suffix, out var chunk) || chunk is < 1 or > maximumChunksToDelete)) continue;
            Response.Cookies.Delete(key, options);
            if (++deleted >= maximumChunksToDelete + (includeBaseCookie ? 1 : 0)) break;
        }
    }
}
