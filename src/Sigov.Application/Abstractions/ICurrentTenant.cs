namespace Sigov.Application.Abstractions;

public interface ICurrentTenant
{
    long? TenantId { get; }
    string? TenantSlug { get; }
    long? EntidadeId { get; }
    long? ExercicioId { get; }
}
