namespace Sigov.Web.Models.Ui;

public sealed record UiButtonViewModel(string Text, string? Url = null, string Variant = "primary", string Type = "button", string? Icon = null, bool Disabled = false);
public sealed record TimelineItemViewModel(string Title, string Description, string Timestamp, string Status = "info");
public sealed record TimelineViewModel(string AccessibleLabel, IReadOnlyList<TimelineItemViewModel> Items);
public sealed record FilterFieldViewModel(string Name, string Label, string Type = "text", string? Placeholder = null);
public sealed record FilterBarViewModel(string Action, IReadOnlyList<FilterFieldViewModel> Fields, string SubmitText = "Filtrar", string ClearUrl = "#");
public sealed record DataTableToolbarViewModel(string Title, string? SearchPlaceholder = null, string? PrimaryActionText = null, string? PrimaryActionUrl = null);
