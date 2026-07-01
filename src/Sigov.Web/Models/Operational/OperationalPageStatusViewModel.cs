namespace Sigov.Web.Models.Operational;

public sealed class OperationalPageStatusViewModel
{
    public string Modulo { get; set; } = string.Empty;
    public string Status { get; set; } = "Parcial";
    public bool UsaDadosReais { get; set; }
    public bool UsaFallback { get; set; }
    public string Mensagem { get; set; } = string.Empty;
}
