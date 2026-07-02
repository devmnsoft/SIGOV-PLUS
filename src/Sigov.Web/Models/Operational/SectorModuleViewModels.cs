namespace Sigov.Web.Models.Operational;

public sealed class SectorModuleViewModel
{
    public string Modulo { get; set; } = string.Empty;
    public string Titulo { get; set; } = string.Empty;
    public string Descricao { get; set; } = string.Empty;
    public string Status { get; set; } = "Em implantação";
    public bool UsaDadosReais { get; set; }
    public bool UsaFallback { get; set; }
    public bool ContemDadosSensiveis { get; set; }
    public IReadOnlyList<string> TabelasDetectadas { get; set; } = Array.Empty<string>();
    public IReadOnlyList<SectorKpiViewModel> Kpis { get; set; } = Array.Empty<SectorKpiViewModel>();
    public IReadOnlyList<SectorRecordViewModel> Registros { get; set; } = Array.Empty<SectorRecordViewModel>();
    public IReadOnlyList<string> Rotas { get; set; } = Array.Empty<string>();
    public IReadOnlyList<string> Pendencias { get; set; } = Array.Empty<string>();
    public string? Filtro { get; set; }
    public string? AcaoPrincipalUrl { get; set; }
    public string? AcaoPrincipalTexto { get; set; }
}

public sealed class SectorKpiViewModel
{
    public string Titulo { get; set; } = string.Empty;
    public string Valor { get; set; } = "0";
    public string Descricao { get; set; } = string.Empty;
    public string Status { get; set; } = "Em implantação";
}

public sealed class SectorRecordViewModel
{
    public string Id { get; set; } = string.Empty;
    public string Titulo { get; set; } = string.Empty;
    public string Subtitulo { get; set; } = string.Empty;
    public string DocumentoMascarado { get; set; } = string.Empty;
    public string Status { get; set; } = "Em implantação";
    public string Origem { get; set; } = "Fallback honesto";
}
