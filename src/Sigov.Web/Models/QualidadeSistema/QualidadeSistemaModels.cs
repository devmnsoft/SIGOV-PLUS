using System.ComponentModel.DataAnnotations;

namespace Sigov.Web.Models.QualidadeSistema;

public sealed class QualidadeDashboardViewModel
{
    public long Abertas { get; set; }
    public long EmAnalise { get; set; }
    public long Criticas { get; set; }
    public long Corrigidas30Dias { get; set; }
    public IReadOnlyList<QualidadeInconsistenciaItem> Recentes { get; set; } = [];
}

public sealed class QualidadeInconsistenciaItem
{
    public long Id { get; set; }
    public string Modulo { get; set; } = "";
    public string Titulo { get; set; } = "";
    public string Severidade { get; set; } = "";
    public string Status { get; set; } = "";
    public string? Responsavel { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}

public sealed class QualidadeFiltro
{
    public string? Modulo { get; set; }
    public string? Severidade { get; set; }
    public string? Status { get; set; }
    public long? ResponsavelId { get; set; }
    [DataType(DataType.Date)] public DateOnly? De { get; set; }
    [DataType(DataType.Date)] public DateOnly? Ate { get; set; }
}

public sealed class QualidadeInconsistenciaInput
{
    [Required, StringLength(80)] public string Modulo { get; set; } = "";
    [Required, StringLength(200)] public string Titulo { get; set; } = "";
    [Required, StringLength(10)] public string Severidade { get; set; } = "MEDIA";
    [StringLength(160)] public string? Tela { get; set; }
    [StringLength(240)] public string? Rota { get; set; }
    [Required, StringLength(4000)] public string Evidencia { get; set; } = "";
    [Required] public long? ResponsavelUsuarioId { get; set; }
}

public sealed class QualidadeTratamentoInput
{
    [Required, StringLength(32)] public string Status { get; set; } = "EM_ANALISE";
    [Required, StringLength(2000), MinLength(10)] public string Justificativa { get; set; } = "";
}

public sealed class QualidadeResponsavelItem { public long Id { get; set; } public string Nome { get; set; } = ""; }
public sealed class QualidadeValidacaoItem { public long Id { get; set; } public string Modulo { get; set; } = ""; public string Referencia { get; set; } = ""; public string Status { get; set; } = ""; public DateTimeOffset? ValidadoEm { get; set; } }
