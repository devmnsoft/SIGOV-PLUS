using Sigov.Web.Models.Visual;

namespace Sigov.Web.Services.Visual;

public sealed class IconRegistry : IIconRegistry
{
    private static readonly IReadOnlyDictionary<string, IconDefinition> Icons = Build();
    public IReadOnlyCollection<IconDefinition> All => Icons.Values;

    public bool TryGet(string name, out IconDefinition definition) =>
        Icons.TryGetValue(name.Trim().ToLowerInvariant(), out definition!);

    private static IReadOnlyDictionary<string, IconDefinition> Build()
    {
        var navigation = new[] { "home", "dashboard", "agenda", "tasks", "favorites", "recent", "commercial", "clients", "services", "work-order", "assets", "maintenance", "sla", "finance", "documents", "contracts", "users", "settings", "help", "notifications" };
        var actions = new[] { "plus", "edit", "delete", "view", "search", "filter", "download", "upload", "print", "refresh", "approve", "reject", "cancel", "confirm", "pause", "play", "complete", "transfer", "copy", "share", "menu", "close", "back", "forward", "theme", "command" };
        var states = new[] { "success", "warning", "error", "info", "locked", "offline", "sync", "expired", "risk", "active", "inactive" };
        return navigation.Select(x => new IconDefinition(x, $"sigov-icon-{x}", "navigation"))
            .Concat(actions.Select(x => new IconDefinition(x, $"sigov-icon-{x}", "action")))
            .Concat(states.Select(x => new IconDefinition(x, $"sigov-icon-{x}", "state")))
            .ToDictionary(x => x.Name, StringComparer.OrdinalIgnoreCase);
    }
}
