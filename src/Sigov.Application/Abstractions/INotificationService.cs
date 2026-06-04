namespace Sigov.Application.Abstractions;

public interface INotificationService
{
    Task NotifyAsync(long usuarioId, string titulo, string mensagem, CancellationToken cancellationToken = default);
}
