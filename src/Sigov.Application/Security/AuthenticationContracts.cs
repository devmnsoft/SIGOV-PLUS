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
    bool DeveAlterarSenha);

public sealed record AuthenticationAccess(
    IReadOnlyCollection<string> Roles,
    IReadOnlyCollection<string> Permissions);

public sealed record AccountReference(long Id, long? TenantId);

public interface IAuthenticationRepository
{
    Task<AuthenticationUser?> FindForLoginAsync(string loginOrEmail, CancellationToken cancellationToken);
    Task<AuthenticationAccess> GetAccessAsync(long userId, CancellationToken cancellationToken);
    Task<AccountReference?> FindActiveAccountAsync(string loginOrEmail, CancellationToken cancellationToken);
    Task StorePasswordResetTokenAsync(AccountReference account, string tokenHash, Guid correlationId, CancellationToken cancellationToken);
    Task<AccountReference?> ConsumePasswordResetTokenAsync(string tokenHash, string passwordHash, CancellationToken cancellationToken);
    Task<string?> GetCurrentPasswordHashAsync(long tenantId, long userId, CancellationToken cancellationToken);
    Task<bool> ChangePasswordAsync(long tenantId, long userId, string passwordHash, CancellationToken cancellationToken);
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
