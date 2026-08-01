using Sigov.Web.Models.Visual;

namespace Sigov.Web.Services.Visual;

public interface IIconRegistry
{
    bool TryGet(string name, out IconDefinition definition);
    IReadOnlyCollection<IconDefinition> All { get; }
}
