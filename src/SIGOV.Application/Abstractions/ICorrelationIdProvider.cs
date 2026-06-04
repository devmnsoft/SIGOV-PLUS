namespace SIGOV.Application.Abstractions;

public interface ICorrelationIdProvider
{
    Guid CorrelationId { get; }
}
