namespace Sigov.Application.Operational;

public sealed class PrazoOperacionalService : IPrazoOperacionalService
{
    private readonly IPrazoOperacionalRepository _repository;

    public PrazoOperacionalService(IPrazoOperacionalRepository repository) => _repository = repository;

    public Task<IReadOnlyList<PrazoOperacionalDto>> ListarVencidosAsync(long tenantId, DateTimeOffset referencia, CancellationToken cancellationToken)
    {
        if (tenantId <= 0) throw new ArgumentOutOfRangeException(nameof(tenantId));
        return _repository.ListarVencidosAsync(tenantId, referencia, cancellationToken);
    }
}
