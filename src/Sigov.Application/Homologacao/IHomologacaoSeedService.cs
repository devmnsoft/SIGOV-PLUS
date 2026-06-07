namespace Sigov.Application.Homologacao;

public interface IHomologacaoSeedService
{
    Task PrepararAsync(HomologacaoSeedOptions options, CancellationToken cancellationToken);
}
