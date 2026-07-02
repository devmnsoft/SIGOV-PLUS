namespace Sigov.Web.Models.Operational;

public sealed record OperationalStatusDescriptor(string Name, string CssClass, string Icon, bool IsTerminal = false);
public sealed record OperationalMetric(string Title, string Value, string Hint, string Status = "Aberto");
public sealed record OperationalItem(long? Id, string Title, string Description, string Module, string Entity, string Status, string? Responsible, DateTimeOffset? DueAt, bool IsFallback, string? ActionUrl = null);
public sealed record OperationalSchemaState(string Table, bool Exists, IReadOnlyList<string> Columns);

public sealed class OperationalHubViewModel
{
    public string AreaKey { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string PrimaryActionText { get; set; } = string.Empty;
    public string PrimaryActionUrl { get; set; } = string.Empty;
    public bool HasRealSchema { get; set; }
    public string FallbackMessage { get; set; } = "Recurso em implantação neste ambiente: o schema operacional ainda não está disponível para persistência real.";
    public IReadOnlyList<OperationalMetric> Metrics { get; set; } = Array.Empty<OperationalMetric>();
    public IReadOnlyList<OperationalItem> Items { get; set; } = Array.Empty<OperationalItem>();
    public IReadOnlyList<OperationalSchemaState> Schema { get; set; } = Array.Empty<OperationalSchemaState>();
    public IReadOnlyList<string> IntegratedModules { get; set; } = Array.Empty<string>();
}

public sealed class WorkflowAdvanceInput
{
    public string? Destino { get; set; }
    public string? Observacao { get; set; }
    public string? Responsavel { get; set; }
    public DateTimeOffset? Prazo { get; set; }
}
