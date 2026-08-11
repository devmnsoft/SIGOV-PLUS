namespace Sigov.Application.Security;

public sealed record AuthenticationUser(
    long Id,
    long? TenantId,
    string Nome,
    string Login,
    string Email,
    string TenantName,
    string PasswordHash,
    bool Ativo,
    bool Bloqueado,
    bool DeveAlterarSenha,
    bool IsDeleted,
    bool TenantAtivo,
    bool TenantIsDeleted,
    int MatchingUsers);

public sealed record AuthenticationAccess(
    IReadOnlyCollection<string> Roles,
    IReadOnlyCollection<string> Permissions);

public sealed record AccountReference(long Id, long? TenantId, string Nome, string Email);

public interface IAuthenticationRepository
{
    Task<AuthenticationUser?> FindForLoginAsync(string loginOrEmail, CancellationToken cancellationToken);
    Task<AuthenticationAccess> GetAccessAsync(long userId, CancellationToken cancellationToken);
    Task<AccountReference?> FindActiveAccountAsync(string loginOrEmail, CancellationToken cancellationToken);
    Task<bool> StorePasswordResetTokenAsync(AccountReference account, string tokenHash, Guid correlationId, CancellationToken cancellationToken);
    Task RevokePasswordResetTokenAsync(string tokenHash, CancellationToken cancellationToken);
    Task<AccountReference?> ConsumePasswordResetTokenAsync(string tokenHash, string passwordHash, CancellationToken cancellationToken);
    Task<string?> GetCurrentPasswordHashAsync(long tenantId, long userId, CancellationToken cancellationToken);
    Task<bool> ChangePasswordAsync(long tenantId, long userId, string passwordHash, CancellationToken cancellationToken);
}

public interface IPasswordRecoveryEmailSender
{
    Task SendAsync(string recipientName, string recipientEmail, string resetUrl, TimeSpan validity, CancellationToken cancellationToken);
}

public interface IPasswordRecoveryService
{
    Task<PasswordRecoveryResult> RequestAsync(string loginOrEmail, Func<string, string> resetUrlFactory, CancellationToken cancellationToken);
}

public enum PasswordRecoveryResult { AccountNotFound, Cooldown, Sent }

public sealed class PasswordRecoveryService(
    IAuthenticationRepository repository,
    IPasswordRecoveryEmailSender emailSender) : IPasswordRecoveryService
{
    private static readonly TimeSpan TokenValidity = TimeSpan.FromMinutes(30);

    public async Task<PasswordRecoveryResult> RequestAsync(string loginOrEmail, Func<string, string> resetUrlFactory, CancellationToken cancellationToken)
    {
        var account = await repository.FindActiveAccountAsync(loginOrEmail.Trim(), cancellationToken).ConfigureAwait(false);
        if (account is null || string.IsNullOrWhiteSpace(account.Email)) return PasswordRecoveryResult.AccountNotFound;

        var token = Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32))
            .Replace('+', '-').Replace('/', '_').TrimEnd('=');
        var tokenHash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(token)));
        var stored = await repository.StorePasswordResetTokenAsync(account, tokenHash, Guid.NewGuid(), cancellationToken).ConfigureAwait(false);
        if (!stored) return PasswordRecoveryResult.Cooldown;

        try
        {
            await emailSender.SendAsync(account.Nome, account.Email, resetUrlFactory(token), TokenValidity, cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            await repository.RevokePasswordResetTokenAsync(tokenHash, cancellationToken).ConfigureAwait(false);
            throw;
        }
        return PasswordRecoveryResult.Sent;
    }
}

public interface IPasswordPolicyService
{
    IReadOnlyCollection<PasswordPolicyError> Validate(string password, string confirmation);
}

public sealed record PasswordPolicyError(string Field, string Message);

public sealed class PasswordPolicyService : IPasswordPolicyService
{
    public IReadOnlyCollection<PasswordPolicyError> Validate(string password, string confirmation)
    {
        var errors = new List<PasswordPolicyError>();
        if (password.Length < 12 || !password.Any(char.IsUpper) || !password.Any(char.IsLower) ||
            !password.Any(char.IsDigit) || !password.Any(character => !char.IsLetterOrDigit(character)))
        {
            errors.Add(new("NovaSenha", "Use no mínimo 12 caracteres, com maiúscula, minúscula, número e caractere especial."));
        }

        if (!string.Equals(password, confirmation, StringComparison.Ordinal))
            errors.Add(new("Confirmacao", "A confirmação não confere."));

        return errors;
    }
}
