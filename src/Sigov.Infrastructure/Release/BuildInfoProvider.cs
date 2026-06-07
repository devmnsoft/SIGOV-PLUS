namespace Sigov.Infrastructure.Release;

public sealed class BuildInfoProvider
{
    public string? CommitSha => EmptyAsNull(Environment.GetEnvironmentVariable("SIGOV_COMMIT_SHA"));
    public string? BuildDate => EmptyAsNull(Environment.GetEnvironmentVariable("SIGOV_BUILD_DATE"));
    public string ReleaseChannel => EmptyAsNull(Environment.GetEnvironmentVariable("SIGOV_RELEASE_CHANNEL")) ?? "stable";

    private static string? EmptyAsNull(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
