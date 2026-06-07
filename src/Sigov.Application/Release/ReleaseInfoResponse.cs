namespace Sigov.Application.Release;

public sealed record ReleaseInfoResponse(
    string Application,
    string Version,
    string ReleaseChannel,
    string? CommitSha,
    string? BuildDate,
    string Environment,
    string Database,
    string Schema);
