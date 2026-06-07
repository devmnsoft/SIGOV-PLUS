namespace Sigov.Web.Models.Ui;

public sealed record BreadcrumbItemViewModel(string Text, string Url, bool IsCurrent = false);

public sealed record PageHeaderViewModel(string Title, string Subtitle, IReadOnlyList<BreadcrumbItemViewModel> Breadcrumbs, string? PrimaryActionText = null, string? PrimaryActionUrl = null);

public sealed record ModuleCardViewModel(string Code, string Name, string Description, string Status, string Icon, string Url);

public sealed record EmptyStateViewModel(string Title, string Message, string Icon = "bi-inbox", string? ActionText = null, string? ActionUrl = null);

public sealed record FormActionViewModel(string Text, string Type, string CssClass);

public sealed record GridColumnViewModel(string Key, string Title, bool Sortable = true);

public sealed record SavedFilterViewModel(string Name, string Module, string Resource, string FiltersJson);
