using Dapper;
using Sigov.Application.Onboarding;
using Sigov.Infrastructure.Persistence.Dapper;

namespace Sigov.Infrastructure.Onboarding;

public sealed class OnboardingRepository(DapperContext context) : IOnboardingRepository
{
    public async Task<OnboardingJourneyRecord?> GetCurrentJourneyAsync(long tenantId, CancellationToken cancellationToken)
    {
        const string sql = """select id, tenant_id as TenantId, nome as Name, status as Status from sigov.onboarding_jornada where tenant_id=@TenantId order by coalesce(updated_at, created_at) desc, id desc limit 1""";
        using var connection = context.CreateConnection();
        return await connection.QuerySingleOrDefaultAsync<OnboardingJourneyRecord>(new CommandDefinition(sql, new { TenantId = tenantId }, cancellationToken: cancellationToken)).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<OnboardingStepRecord>> ListStepsAsync(long tenantId, long journeyId, CancellationToken cancellationToken)
    {
        const string sql = """select id, jornada_id as JourneyId, codigo as Code, nome as Name, descricao as Description, ordem as "Order", status as Status, progresso_percentual as ProgressPercent from sigov.onboarding_etapa where tenant_id=@TenantId and jornada_id=@JourneyId order by ordem, id""";
        using var connection = context.CreateConnection();
        var rows = await connection.QueryAsync<OnboardingStepRecord>(new CommandDefinition(sql, new { TenantId = tenantId, JourneyId = journeyId }, cancellationToken: cancellationToken)).ConfigureAwait(false);
        return rows.AsList();
    }

    public async Task<IReadOnlyList<OnboardingTaskRecord>> ListTasksAsync(long tenantId, long journeyId, CancellationToken cancellationToken)
    {
        const string sql = """select case when metadados->>'etapaId' ~ '^[0-9]+$' then (metadados->>'etapaId')::bigint else 0 end as StepId, codigo as Code, titulo as Title, descricao as Description, coalesce(metadados->>'tipo','checklist') as TaskType, obrigatoria as Required, status as Status, coalesce(rota_destino,'') as Route from sigov.onboarding_tarefa where tenant_id=@TenantId and jornada_id=@JourneyId order by ordem""";
        using var connection = context.CreateConnection();
        var rows = await connection.QueryAsync<OnboardingTaskRecord>(new CommandDefinition(sql, new { TenantId = tenantId, JourneyId = journeyId }, cancellationToken: cancellationToken)).ConfigureAwait(false);
        return rows.AsList();
    }
}
