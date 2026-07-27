using System.Text.RegularExpressions;
using Sigov.Testing;
using Xunit;

namespace Sigov.UnitTests.Architecture;

public sealed class RepositoryPathContractTests
{
    private static readonly Regex FragilePath = new(
        "Path\\.Combine\\(\\s*\"\\.\\.\"|(?:\\.\\./){2,}|Directory\\.GetCurrentDirectory\\(\\).*?(?:src|database|tests)",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.Singleline);

    [Fact]
    public void Tests_Nao_Devem_Usar_Paths_Relativos_Ascendentes()
    {
        var testsRoot = TestRepoPath.Get("tests");
        var violations = Directory.EnumerateFiles(testsRoot, "*.cs", SearchOption.AllDirectories)
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                && !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .Where(path => FragilePath.IsMatch(File.ReadAllText(path)))
            .Select(path => Path.GetRelativePath(TestRepoPath.Root, path))
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToArray();

        Assert.True(violations.Length == 0,
            "Testes devem usar TestRepoPath/RepositoryPathResolver. Violações: " + string.Join(", ", violations));
    }
}
