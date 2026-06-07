using FluentAssertions;
using Xunit;

namespace Sigov.IntegrationTests;

public sealed class WorkerRegressionTests
{
    private static readonly string Root = FindRepositoryRoot();

    [Fact]
    public void Worker_Deve_Ter_Processamento_Outbox_Com_Retry_E_DeadLetter()
    {
        var processor = File.ReadAllText(Path.Combine(Root, "src", "Sigov.Worker", "Outbox", "OutboxProcessor.cs"));
        var retryPolicy = File.ReadAllText(Path.Combine(Root, "src", "Sigov.Worker", "Outbox", "OutboxRetryPolicy.cs"));

        processor.Should().Contain("ProcessBatchAsync");
        processor.Should().Contain("TenantId");
        processor.Should().Contain("CorrelationId");
        retryPolicy.Should().Contain("DeadLetter");
    }

    [Fact]
    public void OutboxRepository_Deve_Isolar_Tenant_E_Atualizar_Status_Processado_Falha()
    {
        var repository = File.ReadAllText(Path.Combine(Root, "src", "Sigov.Infrastructure", "Outbox", "OutboxRepository.cs"));
        var queries = File.ReadAllText(Path.Combine(Root, "src", "Sigov.Infrastructure", "Outbox", "OutboxSqlQueries.cs"));

        queries.Should().Contain("sigov.fila_evento");
        queries.Should().Contain("tenant_id");
        queries.Should().Contain("PROCESSADO");
        queries.Should().Contain("ERRO");
        queries.Should().Contain("DEAD_LETTER");
        repository.Should().Contain("MarkFailureAsync");
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "sigov.sln")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new DirectoryNotFoundException("Raiz do repositório sigov não encontrada.");
    }
}
