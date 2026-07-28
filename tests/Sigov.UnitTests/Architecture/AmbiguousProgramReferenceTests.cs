using FluentAssertions;

namespace Sigov.UnitTests.Architecture;

public sealed class AmbiguousProgramReferenceTests
{
    [Fact]
    public void Tests_Nao_Devem_Referenciar_Program_Ambiguo()
    {
        var testRoots = new[]
        {
            TestRepoPath.Get("tests/Sigov.ApiTests"),
            TestRepoPath.Get("tests/Sigov.IntegrationTests")
        };

        var violations = testRoots
            .SelectMany(root => Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories))
            .Where(path => File.ReadAllText(path).Contains("WebApplicationFactory<Program>", StringComparison.Ordinal))
            .Select(path => Path.GetRelativePath(TestRepoPath.Root, path))
            .ToArray();

        violations.Should().BeEmpty(
            "host tests must use SigovApiFactory or SigovWebFactory instead of an ambiguous global Program");
    }
}
