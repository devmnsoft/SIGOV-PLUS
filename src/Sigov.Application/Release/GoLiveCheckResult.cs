namespace Sigov.Application.Release;

public enum GoLiveCheckStatus
{
    Pass = 0,
    Warn = 1,
    Fail = 2
}

public sealed record GoLiveCheck(string Name, GoLiveCheckStatus Status, string Message);

public sealed record GoLiveCheckResult(IReadOnlyCollection<GoLiveCheck> Checks)
{
    public bool HasFailures => Checks.Any(check => check.Status == GoLiveCheckStatus.Fail);
}
