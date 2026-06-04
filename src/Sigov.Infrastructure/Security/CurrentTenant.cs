using Sigov.Application.Abstractions;

namespace Sigov.Infrastructure.Security;

public sealed class CurrentTenant : ICurrentTenant
{
    public long? EntidadeId => null;
    public long? ExercicioId => null;
}
