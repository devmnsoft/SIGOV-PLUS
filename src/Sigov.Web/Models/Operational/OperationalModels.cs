namespace Sigov.Web.Models.Operational;

public sealed record ModuleKpi(string Label, string Value, string Hint, string Variant = "primary");
public sealed record QuickAction(string Label, string Url, string Icon = "↗", bool Critical = false);
public sealed record TimelineStep(string Title, string Description, string Status, string DateLabel);
public sealed record DemoRecord(long Id, string Codigo, string Nome, string Status, string Responsavel, string AtualizadoEm, string Documento = "");

public sealed class OperationalModuleViewModel
{
    public OperationalPageStatusViewModel PageStatus { get; set; } = new();
    public IReadOnlyList<string> SchemaTables { get; set; } = Array.Empty<string>();
    public string Area { get; set; } = "Operação";
    public string ModuleKey { get; set; } = "modulo";
    public string Title { get; set; } = "Módulo operacional";
    public string Description { get; set; } = "Fluxo mínimo demonstrável com dados seguros para POC.";
    public string Purpose { get; set; } = "Organiza rotinas, indicadores, filtros, auditoria e ações rápidas do módulo.";
    public string Status { get; set; } = "Em implantação assistida";
    public string ManualUrl { get; set; } = "/Manual";
    public string CurrentScreen { get; set; } = "Dashboard";
    public string HelpText { get; set; } = "Use os filtros para localizar registros, selecione itens para ações em massa e consulte detalhes antes de ações críticas.";
    public IReadOnlyList<ModuleKpi> Kpis { get; set; } = Array.Empty<ModuleKpi>();
    public IReadOnlyList<QuickAction> Actions { get; set; } = Array.Empty<QuickAction>();
    public IReadOnlyList<string> NextSteps { get; set; } = Array.Empty<string>();
    public IReadOnlyList<TimelineStep> Timeline { get; set; } = Array.Empty<TimelineStep>();
    public IReadOnlyList<DemoRecord> Records { get; set; } = Array.Empty<DemoRecord>();
    public bool ShowLgpdWarning { get; set; }
    public string EntitySingular { get; set; } = "registro";
    public string EntityPlural { get; set; } = "registros";
}
