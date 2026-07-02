namespace Sigov.Web.Models.Operational;

public sealed record ModuleKpi(string Label, string Value, string Hint, string Variant = "primary");
public sealed record QuickAction(string Label, string Url, string Icon = "↗", bool Critical = false);
public sealed record TimelineStep(string Title, string Description, string Status, string DateLabel);
public sealed record DemoRecord(long Id, string Codigo, string Nome, string Status, string Responsavel, string AtualizadoEm, string Documento = "");

public sealed class OperationalModuleViewModel
{
    public OperationalPageStatusViewModel PageStatus { get; init; } = new();
    public IReadOnlyList<string> SchemaTables { get; init; } = Array.Empty<string>();
    public string Area { get; init; } = "Operação";
    public string ModuleKey { get; init; } = "modulo";
    public string Title { get; init; } = "Módulo operacional";
    public string Description { get; init; } = "Fluxo mínimo demonstrável com dados seguros para POC.";
    public string Purpose { get; init; } = "Organiza rotinas, indicadores, filtros, auditoria e ações rápidas do módulo.";
    public string Status { get; init; } = "Em implantação assistida";
    public string ManualUrl { get; init; } = "/Manual";
    public string CurrentScreen { get; init; } = "Dashboard";
    public string HelpText { get; init; } = "Use os filtros para localizar registros, selecione itens para ações em massa e consulte detalhes antes de ações críticas.";
    public IReadOnlyList<ModuleKpi> Kpis { get; init; } = Array.Empty<ModuleKpi>();
    public IReadOnlyList<QuickAction> Actions { get; init; } = Array.Empty<QuickAction>();
    public IReadOnlyList<string> NextSteps { get; init; } = Array.Empty<string>();
    public IReadOnlyList<TimelineStep> Timeline { get; init; } = Array.Empty<TimelineStep>();
    public IReadOnlyList<DemoRecord> Records { get; init; } = Array.Empty<DemoRecord>();
    public bool ShowLgpdWarning { get; init; }
    public string EntitySingular { get; init; } = "registro";
    public string EntityPlural { get; init; } = "registros";
}
