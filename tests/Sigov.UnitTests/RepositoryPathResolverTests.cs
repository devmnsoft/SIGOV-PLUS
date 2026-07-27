using FluentAssertions;
using Sigov.Testing;
using Xunit;

namespace Sigov.UnitTests;

public sealed class RepositoryPathResolverTests
{
    [Fact]
    [Trait("Category", "Unit")]
    public void ResolveFromNestedDirectoryFindsRepositoryRoots()
    {
        var nested = Path.Combine(TestRepoPath.Root, "tests", "Sigov.UnitTests");

        var sut = new RepositoryPathResolver(nested);

        File.Exists(Path.Combine(sut.RepoRoot, "sigov.sln")).Should().BeTrue();
        sut.SrcRoot.Should().Be(Path.Combine(sut.RepoRoot, "src"));
        sut.TestsRoot.Should().Be(Path.Combine(sut.RepoRoot, "tests"));
        sut.MigrationsRoot.Should().Be(Path.Combine(sut.DatabaseRoot, "postgres", "migrations"));
        sut.WebRoot.Should().Be(Path.Combine(sut.SrcRoot, "Sigov.Web"));
        sut.ApiRoot.Should().Be(Path.Combine(sut.SrcRoot, "Sigov.Api"));
        sut.WorkerRoot.Should().Be(Path.Combine(sut.SrcRoot, "Sigov.Worker"));
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void MissingRepositoryThrowsInsteadOfUsingCurrentDirectory()
    {
        var isolated = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(isolated);

        try
        {
            var action = () => new RepositoryPathResolver(isolated);
            action.Should().Throw<DirectoryNotFoundException>();
        }
        finally
        {
            Directory.Delete(isolated);
        }
    }
}
