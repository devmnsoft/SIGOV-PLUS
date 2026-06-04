namespace Sigov.Application.Abstractions;

public interface ICurrentUser
{
    long? UsuarioId { get; }
    string? Nome { get; }
    bool IsAuthenticated { get; }
}
