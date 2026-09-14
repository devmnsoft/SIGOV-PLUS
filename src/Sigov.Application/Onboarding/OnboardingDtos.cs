namespace Sigov.Application.Onboarding;

public enum OnboardingStatus { Pendente, EmAndamento, Concluido, Bloqueado }

public sealed record OnboardingTaskDto(string Title, string Description, string TaskType, bool Required, OnboardingStatus Status, string Route);
public sealed record OnboardingStepDto(string Code, string Name, string Description, int Order, OnboardingStatus Status, decimal ProgressPercent, IReadOnlyList<OnboardingTaskDto> Tasks);
public sealed record OnboardingJourneyDto(long TenantId, string Name, OnboardingStatus Status, decimal ProgressPercent, IReadOnlyList<OnboardingStepDto> Steps);

public sealed record OnboardingJourneyRecord(long Id, long TenantId, string Name, string Status);
public sealed record OnboardingStepRecord(long Id, long JourneyId, string Code, string Name, string Description, int Order, string Status, decimal ProgressPercent);
public sealed record OnboardingTaskRecord(long StepId, string Code, string Title, string Description, string TaskType, bool Required, string Status, string Route);

public interface IOnboardingRepository
{
    Task<OnboardingJourneyRecord?> GetCurrentJourneyAsync(long tenantId, CancellationToken cancellationToken);
    Task<IReadOnlyList<OnboardingStepRecord>> ListStepsAsync(long tenantId, long journeyId, CancellationToken cancellationToken);
    Task<IReadOnlyList<OnboardingTaskRecord>> ListTasksAsync(long tenantId, long journeyId, CancellationToken cancellationToken);
}

public interface IOnboardingService
{
    Task<OnboardingJourneyDto?> GetJourneyAsync(long tenantId, CancellationToken cancellationToken = default);
}

public sealed class OnboardingService(IOnboardingRepository repository) : IOnboardingService
{
    public static decimal CalculateProgress(IEnumerable<OnboardingStepDto> steps)
    {
        var materialized = steps.ToArray();
        return materialized.Length == 0 ? 0m : Math.Round(materialized.Average(step => step.ProgressPercent), 2, MidpointRounding.AwayFromZero);
    }

    public async Task<OnboardingJourneyDto?> GetJourneyAsync(long tenantId, CancellationToken cancellationToken = default)
    {
        if (tenantId <= 0) throw new ArgumentOutOfRangeException(nameof(tenantId));
        var journey = await repository.GetCurrentJourneyAsync(tenantId, cancellationToken).ConfigureAwait(false);
        if (journey is null) return null;

        var steps = await repository.ListStepsAsync(tenantId, journey.Id, cancellationToken).ConfigureAwait(false);
        var tasks = await repository.ListTasksAsync(tenantId, journey.Id, cancellationToken).ConfigureAwait(false);
        var result = steps.Select(step => new OnboardingStepDto(step.Code, step.Name, step.Description, step.Order,
            ParseStatus(step.Status), step.ProgressPercent,
            tasks.Where(task => task.StepId == step.Id || (task.StepId == 0 && task.Code == step.Code)).Select(task => new OnboardingTaskDto(task.Title, task.Description,
                task.TaskType, task.Required, ParseStatus(task.Status), task.Route)).ToArray())).ToArray();
        var progress = CalculateProgress(result);
        return new OnboardingJourneyDto(tenantId, journey.Name, ParseStatus(journey.Status), progress, result);
    }

    private static OnboardingStatus ParseStatus(string value) => value.Trim().ToUpperInvariant() switch
    {
        "CONCLUIDO" or "CONCLUÍDO" => OnboardingStatus.Concluido,
        "EM_ANDAMENTO" or "EM ANDAMENTO" => OnboardingStatus.EmAndamento,
        "BLOQUEADO" => OnboardingStatus.Bloqueado,
        _ => OnboardingStatus.Pendente
    };
}
