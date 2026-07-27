namespace Sigov.Application.Operational;

public sealed class OperationalConcurrencyException : Exception
{
    public OperationalConcurrencyException(long tarefaId, long expectedVersion)
        : base($"A tarefa {tarefaId} foi alterada por outro usuário (versão esperada: {expectedVersion}).")
    {
        TarefaId = tarefaId;
        ExpectedVersion = expectedVersion;
    }

    public long TarefaId { get; }
    public long ExpectedVersion { get; }
}
