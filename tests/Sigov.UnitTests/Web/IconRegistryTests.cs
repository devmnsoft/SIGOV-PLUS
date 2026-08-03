using FluentAssertions;
using Sigov.Web.Services.Visual;

namespace Sigov.UnitTests.Web;

public sealed class IconRegistryTests
{
    private readonly IconRegistry _registry = new();

    [Fact]
    public void All_is_a_stable_read_only_collection()
    {
        _registry.All.Should().NotBeEmpty();
        _registry.All.Should().BeSameAs(_registry.All);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("unknown")]
    public void TryGet_rejects_invalid_or_unknown_names_without_throwing(string? name)
    {
        var action = () => _registry.TryGet(name!, out _);

        action.Should().NotThrow();
        _registry.TryGet(name!, out var definition).Should().BeFalse();
        definition.Should().NotBeNull();
    }

    [Fact]
    public void TryGet_is_trimmed_and_case_insensitive()
    {
        _registry.TryGet(" HOME ", out var definition).Should().BeTrue();
        definition.Name.Should().Be("home");
    }
}
