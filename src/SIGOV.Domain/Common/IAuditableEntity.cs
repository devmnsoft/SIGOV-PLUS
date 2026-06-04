namespace SIGOV.Domain.Common;

public interface IAuditableEntity
{
    DateTimeOffset CreatedAt { get; }
    long? CreatedBy { get; }
    DateTimeOffset? UpdatedAt { get; }
    long? UpdatedBy { get; }
}
