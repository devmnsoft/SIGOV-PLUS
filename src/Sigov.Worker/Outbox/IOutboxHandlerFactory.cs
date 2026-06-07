namespace Sigov.Worker.Outbox;

public interface IOutboxHandlerFactory
{
    IOutboxHandler Resolve(string tipoEvento);
}
