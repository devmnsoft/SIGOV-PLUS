using Sigov.Web.Models.Visual;

namespace Sigov.Web.Services.Visual;

public interface IVisualAssetProvider
{
    bool TryResolve(string asset, out VisualAssetDescriptor descriptor);
}
