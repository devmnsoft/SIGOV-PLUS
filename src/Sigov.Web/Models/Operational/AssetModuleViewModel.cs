namespace Sigov.Web.Models.Operational;

public sealed class AssetModuleViewModel
{
    public string Modulo { get; set; } = string.Empty;
    public string Titulo { get; set; } = string.Empty;
    public string Descricao { get; set; } = string.Empty;
    public string Status { get; set; } = "Em implantação";
    public bool UsaDadosReais { get; set; }
    public bool UsaFallback { get; set; }
    public IReadOnlyList<string> TabelasDetectadas { get; set; } = Array.Empty<string>();
    public IReadOnlyList<AssetKpiViewModel> Kpis { get; set; } = Array.Empty<AssetKpiViewModel>();
    public IReadOnlyList<AssetRecordViewModel> Registros { get; set; } = Array.Empty<AssetRecordViewModel>();
}

public sealed record AssetKpiViewModel(string Rotulo, string Valor, string Ajuda, string Variante = "primary");
public sealed record AssetRecordViewModel(long Id, string Codigo, string Descricao, string Status, string Responsavel, string AtualizadoEm, string Origem = "Fallback honesto");
