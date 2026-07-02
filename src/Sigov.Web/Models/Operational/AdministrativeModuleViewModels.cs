namespace Sigov.Web.Models.Operational;

public sealed class AdministrativeModuleViewModel
{
    public string Modulo { get; set; } = string.Empty;
    public string Titulo { get; set; } = string.Empty;
    public string Descricao { get; set; } = string.Empty;
    public string Status { get; set; } = "Em implantação";
    public bool UsaDadosReais { get; set; }
    public bool UsaFallback { get; set; }
    public IReadOnlyList<string> TabelasDetectadas { get; set; } = Array.Empty<string>();
    public IReadOnlyList<AdministrativeKpiViewModel> Kpis { get; set; } = Array.Empty<AdministrativeKpiViewModel>();
    public IReadOnlyList<AdministrativeRecordViewModel> Registros { get; set; } = Array.Empty<AdministrativeRecordViewModel>();
}

public sealed record AdministrativeKpiViewModel(string Rotulo, string Valor, string Ajuda, string Variante = "primary");
public sealed record AdministrativeRecordViewModel(long Id, string Codigo, string Descricao, string Status, string Responsavel, string AtualizadoEm);
