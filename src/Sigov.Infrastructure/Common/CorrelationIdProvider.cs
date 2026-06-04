using Sigov.Application.Abstractions;

namespace Sigov.Infrastructure.Common;

public sealed class CorrelationIdProvider : ICorrelationIdProvider
{
    private readonly Guid _correlationId = Guid.NewGuid();

    public Guid CorrelationId => _correlationId;
}
