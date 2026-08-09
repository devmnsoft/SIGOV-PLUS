namespace Sigov.Application.Abstractions;

public interface ICurrentUser
{
    long? UsuarioId { get; }
    long? UserId { get; }
    long? TenantId { get; }
    string? Nome { get; }
    string? Login { get; }
    string? Email { get; }
    string? TenantName { get; }
    IReadOnlyCollection<string> Roles { get; }
    IReadOnlyCollection<string> Permissions { get; }
    bool IsAuthenticated { get; }
}
