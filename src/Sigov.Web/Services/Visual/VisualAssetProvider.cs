using Sigov.Web.Models.Visual;

namespace Sigov.Web.Services.Visual;

public sealed class VisualAssetProvider : IVisualAssetProvider
{
    private static readonly VisualAssetDescriptor Fallback = new("fallback", "/img/illustrations/empty-search.svg", 640, 420, "/img/illustrations/empty-search.svg");
    private static readonly IReadOnlyDictionary<string, VisualAssetDescriptor> Assets = new Dictionary<string, VisualAssetDescriptor>(StringComparer.OrdinalIgnoreCase)
    {
        ["login-hero"] = new("login-hero", "/img/illustrations/login-platform.svg", 960, 720, Fallback.Path),
        ["empty-search"] = Fallback,
        ["empty-tasks"] = new("empty-tasks", "/img/illustrations/empty-tasks.svg", 640, 420, Fallback.Path),
        ["empty-notifications"] = new("empty-notifications", "/img/illustrations/empty-notifications.svg", 640, 420, Fallback.Path),
        ["access-denied"] = new("access-denied", "/img/illustrations/access-denied.svg", 640, 420, Fallback.Path),
        ["service-unavailable"] = new("service-unavailable", "/img/illustrations/service-unavailable.svg", 640, 420, Fallback.Path)
    };

    public bool TryResolve(string asset, out VisualAssetDescriptor descriptor) => Assets.TryGetValue(asset, out descriptor!);
}
