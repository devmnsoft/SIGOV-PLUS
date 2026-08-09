using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Authorization;
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
            const string sql = @"select u.id, u.tenant_id as TenantId, coalesce(u.nome, u.login) as Nome, u.login, u.email, u.senha_hash as SenhaHash,
       u.ativo, u.bloqueado, coalesce(u.deve_alterar_senha, false) as DeveAlterarSenha
from sigov.usuario u
left join sigov.tenant t on t.id = u.tenant_id
where u.is_deleted = false and (u.tenant_id is null or (t.ativo and not t.is_deleted))
  and (lower(u.login) = lower(@Login) or lower(u.email) = lower(@Login))
limit 1;";
            using var connection = _connectionFactory.CreateConnection();
            var user = await connection.QuerySingleOrDefaultAsync<LoginUserRow>(new CommandDefinition(sql, new { model.Login }, cancellationToken: cancellationToken)).ConfigureAwait(false);
            var valid = user is not null && user.Ativo && !user.Bloqueado && _passwordHashService.VerifyPassword(model.Senha, user.SenhaHash);
            await _auditTrail.RegistrarAsync(user?.TenantId, user?.Id, valid ? "LOGIN_SUCESSO" : "LOGIN_FALHA", "sigov.usuario", user?.Id.ToString(), null, new { login = model.Login }, ip, Request.Headers["User-Agent"].ToString(), correlationId, cancellationToken).ConfigureAwait(false);
            _logger.LogInformation("Tentativa de login SIGOV: {Resultado}. CorrelationId={CorrelationId}", valid ? "sucesso" : "falha", correlationId);

            if (!valid || user is null)
            {
                model.MensagemErro = "Credenciais inválidas ou usuário bloqueado.";
                return View(model);
            }

            var access = await LoadAccessAsync(connection, user.Id, cancellationToken).ConfigureAwait(false);
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
            const string findSql = @"select u.id, u.tenant_id as TenantId from sigov.usuario u left join sigov.tenant t on t.id=u.tenant_id
where u.ativo and not u.bloqueado and not u.is_deleted and (u.tenant_id is null or (t.ativo and not t.is_deleted))
and (lower(u.login)=lower(@Value) or lower(u.email)=lower(@Value)) limit 1;";
            using var connection = _connectionFactory.CreateConnection();
            var account = await connection.QuerySingleOrDefaultAsync<RecoveryUserRow>(new CommandDefinition(findSql, new { Value = model.LoginOuEmail }, cancellationToken: cancellationToken)).ConfigureAwait(false);
            if (account is not null)
            {
                var token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)).Replace('+', '-').Replace('/', '_').TrimEnd('=');
                var tokenHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
                const string saveSql = @"insert into sigov.senha_redefinicao_token(tenant_id, usuario_id, token_hash, expira_at, correlation_id)
values(@TenantId,@UsuarioId,@TokenHash,now()+interval '30 minutes',@CorrelationId::uuid);";
                await connection.ExecuteAsync(new CommandDefinition(saveSql, new { account.TenantId, UsuarioId = account.Id, TokenHash = tokenHash, CorrelationId = Guid.Parse(HttpContext.TraceIdentifier) }, cancellationToken: cancellationToken)).ConfigureAwait(false);
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
        ValidatePassword(model.NovaSenha ?? string.Empty, model.Confirmacao ?? string.Empty);
        if (!ModelState.IsValid) return View(model);
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(model.Token)));
        using var connection = _connectionFactory.CreateConnection();
        const string sql = @"with valid_token as (
 update sigov.senha_redefinicao_token set usado_at=now()
 where id=(select id from sigov.senha_redefinicao_token where token_hash=@TokenHash and usado_at is null and expira_at>now() order by created_at desc limit 1)
 returning usuario_id)
update sigov.usuario u set senha_hash=@PasswordHash, deve_alterar_senha=false, updated_at=now()
from valid_token t where u.id=t.usuario_id returning u.id, u.tenant_id;";
        var changed = await connection.QuerySingleOrDefaultAsync<RecoveryUserRow>(new CommandDefinition(sql, new { TokenHash = hash, PasswordHash = _passwordHashService.HashPassword(model.NovaSenha) }, cancellationToken: ct)).ConfigureAwait(false);
        if (changed is null) { ModelState.AddModelError(string.Empty, "Link inválido ou expirado."); return View(model); }
        await _auditTrail.RegistrarAsync(changed.TenantId, changed.Id, "SENHA_REDEFINIDA", "sigov.usuario", changed.Id.ToString(), null, new { origem = "recuperacao" }, null, null, HttpContext.TraceIdentifier, ct).ConfigureAwait(false);
        return RedirectToAction(nameof(Login));
    }

    private async Task<IActionResult> AlterarSenhaCore(ChangePasswordViewModel model, CancellationToken ct)
    {
        ValidatePassword(model.NovaSenha ?? string.Empty, model.Confirmacao ?? string.Empty);
        if (!ModelState.IsValid) return View("AlterarSenha", model);
        var id = CurrentUserId();
        if (id is null) return Challenge();
        using var connection = _connectionFactory.CreateConnection();
        var currentHash = await connection.ExecuteScalarAsync<string>(new CommandDefinition("select senha_hash from sigov.usuario where id=@Id and ativo and not is_deleted", new { Id = id.Value }, cancellationToken: ct)).ConfigureAwait(false);
        if (string.IsNullOrEmpty(currentHash) || !_passwordHashService.VerifyPassword(model.SenhaAtual, currentHash)) { ModelState.AddModelError(nameof(model.SenhaAtual), "Senha atual inválida."); return View("AlterarSenha", model); }
        if (_passwordHashService.VerifyPassword(model.NovaSenha, currentHash)) { ModelState.AddModelError(nameof(model.NovaSenha), "A nova senha deve ser diferente da atual."); return View("AlterarSenha", model); }
        await connection.ExecuteAsync(new CommandDefinition("update sigov.usuario set senha_hash=@Hash, deve_alterar_senha=false, updated_at=now() where id=@Id", new { Id = id.Value, Hash = _passwordHashService.HashPassword(model.NovaSenha) }, cancellationToken: ct)).ConfigureAwait(false);
        await _auditTrail.RegistrarAsync(long.TryParse(User.FindFirstValue("tenant_id"), out var tenant) ? tenant : null, id, "SENHA_ALTERADA", "sigov.usuario", id.ToString(), null, new { origem = "usuario" }, null, null, HttpContext.TraceIdentifier, ct).ConfigureAwait(false);
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme).ConfigureAwait(false);
        TempData["Success"] = "Senha alterada. Entre novamente.";
        return RedirectToAction(nameof(Login));
    }

    private void ValidatePassword(string password, string confirmation)
    {
        if (password.Length < 12 || !password.Any(char.IsUpper) || !password.Any(char.IsLower) || !password.Any(char.IsDigit) || !password.Any(ch => !char.IsLetterOrDigit(ch)))
            ModelState.AddModelError(nameof(ChangePasswordViewModel.NovaSenha), "Use no mínimo 12 caracteres, com maiúscula, minúscula, número e especial.");
        if (!string.Equals(password, confirmation, StringComparison.Ordinal)) ModelState.AddModelError(nameof(ChangePasswordViewModel.Confirmacao), "A confirmação não confere.");
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

    private static async Task<UserAccess> LoadAccessAsync(System.Data.IDbConnection connection, long userId, CancellationToken cancellationToken)
    {
        const string sql = @"
select distinct access_value
from (
    select pn.codigo as access_value
      from sigov.usuario u
      join sigov.perfil_nivel pn on pn.codigo = upper(trim(u.tipo_usuario)) and pn.ativo
     where u.id = @UserId
    union
    select pn.codigo
      from sigov.usuario_grupo ug
      join sigov.grupo_perfil gp on gp.grupo_acesso_id = ug.grupo_acesso_id and not gp.is_deleted
      join sigov.perfil_acesso pa on pa.id = gp.perfil_acesso_id and pa.ativo and not pa.is_deleted
      join sigov.perfil_nivel pn on pn.codigo = upper(trim(pa.codigo_externo)) and pn.ativo
     where ug.usuario_id = @UserId and not ug.is_deleted
) roles
where access_value is not null;

select distinct p.chave
  from sigov.usuario_grupo ug
  join sigov.grupo_perfil gp on gp.grupo_acesso_id = ug.grupo_acesso_id and not gp.is_deleted
  join sigov.perfil_acesso pa on pa.id = gp.perfil_acesso_id and pa.ativo and not pa.is_deleted
  join sigov.perfil_permissao pp on pp.perfil_acesso_id = pa.id
  join sigov.permissao p on p.id = pp.permissao_id and p.ativo and not p.is_deleted
 where ug.usuario_id = @UserId and not ug.is_deleted;";

        using var result = await connection.QueryMultipleAsync(new CommandDefinition(sql, new { UserId = userId }, cancellationToken: cancellationToken)).ConfigureAwait(false);
        var roles = (await result.ReadAsync<string>().ConfigureAwait(false)).Where(IsSafeClaimValue).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        var permissions = (await result.ReadAsync<string>().ConfigureAwait(false)).Where(IsSafeClaimValue).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        return new UserAccess(roles, permissions);
    }

    private static bool IsSafeClaimValue(string? value) => !string.IsNullOrWhiteSpace(value) && value.Length <= 150;

    private static async Task RegistrarAuditoriaAsync(System.Data.IDbConnection connection, string acao, long? tenantId, long? usuarioId, string login, string? ip, string correlationId, CancellationToken cancellationToken)
    {
        const string sql = @"insert into sigov.auditoria_evento (tenant_id, usuario_id, acao, entidade, entidade_id, ip, user_agent, depois, correlation_id)
values (@TenantId, @UsuarioId, @Acao, 'sigov.usuario', @EntidadeId, @Ip, null, jsonb_build_object('login', @Login), @CorrelationId::uuid);";
        var correlation = Guid.TryParse(correlationId, out var parsed) ? parsed : Guid.NewGuid();
        await connection.ExecuteAsync(new CommandDefinition(sql, new { TenantId = tenantId, UsuarioId = usuarioId, Acao = acao, EntidadeId = usuarioId?.ToString(), Ip = ip, Login = login, CorrelationId = correlation }, cancellationToken: cancellationToken)).ConfigureAwait(false);
    }

    private sealed record LoginUserRow(long Id, long? TenantId, string Nome, string Login, string Email, string SenhaHash, bool Ativo, bool Bloqueado, bool DeveAlterarSenha);
    private sealed record RecoveryUserRow(long Id, long? TenantId);
    private sealed record UserAccess(IReadOnlyCollection<string> Roles, IReadOnlyCollection<string> Permissions);
}
