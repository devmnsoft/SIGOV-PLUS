namespace Sigov.Domain.Common;

public interface ISoftDelete
{
    bool IsDeleted { get; }
    DateTimeOffset? DeletedAt { get; }
    long? DeletedBy { get; }
}
