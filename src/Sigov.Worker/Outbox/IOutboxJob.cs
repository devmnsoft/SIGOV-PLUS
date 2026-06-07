namespace Sigov.Worker.Outbox;

public interface IOutboxJob
{
    Task RunAsync(CancellationToken cancellationToken);
}
