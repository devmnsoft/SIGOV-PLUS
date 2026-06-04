namespace Sigov.Application.Abstractions;

public interface ICurrentTenant
{
    long? EntidadeId { get; }
    long? ExercicioId { get; }
}
