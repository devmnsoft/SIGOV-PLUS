namespace SIGOV.Application.Abstractions;

public interface IAuditService
{
    Task RegistrarAsync(string modulo, string acao, string tabela, string chave, object? anterior, object? novo, CancellationToken cancellationToken = default);
}
