namespace Sigov.Application.Release;

public interface ISmokeTestPlanService
{
    IReadOnlyCollection<string> GetRequiredEndpoints(string environment);
}
