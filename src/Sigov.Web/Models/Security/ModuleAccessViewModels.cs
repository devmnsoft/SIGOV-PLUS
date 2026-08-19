namespace Sigov.Web.Models.Security;

public sealed record ModuleAccessCardViewModel(
    string Code,
    string Name,
    string Category,
    string Description,
    string Icon,
    string Route,
    string Status,
    bool Allowed,
    string BlockReason,
    bool HasSensitiveData,
    IReadOnlyList<string> Actions);

public sealed record AccessMatrixRowViewModel(
    string Module,
    string Resource,
    string Action,
    bool Allowed,
    string Reason);

public sealed class AccessMatrixViewModel
{
    public string Profile { get; init; } = string.Empty;
    public IReadOnlyList<string> Profiles { get; init; } = Array.Empty<string>();
    public IReadOnlyList<AccessMatrixRowViewModel> Rows { get; init; } = Array.Empty<AccessMatrixRowViewModel>();
    public bool CanExport { get; init; }
}
