namespace Sigov.Application.Homologacao;

public sealed class HomologacaoSeedService : IHomologacaoSeedService
{
    public Task PrepararAsync(HomologacaoSeedOptions options, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(options);
        cancellationToken.ThrowIfCancellationRequested();
        return Task.CompletedTask;
    }
}
