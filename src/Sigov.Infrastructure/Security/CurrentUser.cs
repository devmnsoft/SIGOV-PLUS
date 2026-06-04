using Sigov.Application.Abstractions;

namespace Sigov.Infrastructure.Security;

public sealed class CurrentUser : ICurrentUser
{
    public long? UsuarioId => null;
    public string? Nome => null;
    public bool IsAuthenticated => false;
}
