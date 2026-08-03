using FluentAssertions;
using Sigov.Testing;

namespace Sigov.UnitTests.Ui;

public sealed class CanonicalIconographyTests
{
    [Fact]
    public void Product_views_must_not_reference_bootstrap_icon_classes()
    {
        var viewsRoot = TestRepoPath.Get("src/Sigov.Web/Views");
        var violations = Directory.EnumerateFiles(viewsRoot, "*.cshtml", SearchOption.AllDirectories)
            .Where(path => File.ReadAllText(path).Contains("bi-", StringComparison.OrdinalIgnoreCase))
            .Select(path => Path.GetRelativePath(viewsRoot, path))
            .OrderBy(path => path)
            .ToArray();

        violations.Should().BeEmpty("the SIGOV SVG registry is the sole product icon system");
    }
}
