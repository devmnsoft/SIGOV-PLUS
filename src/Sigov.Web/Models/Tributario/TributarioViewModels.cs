using System.ComponentModel.DataAnnotations;

namespace Sigov.Web.Models.Tributario;

public sealed class TributarioDashboardViewModel
{
    public int ContribuintesAtivos { get; set; }
    public int ImoveisAtivos { get; set; }
    public int EmpresasAtivas { get; set; }
    public int LancamentosExercicio { get; set; }
    public decimal TotalLancado { get; set; }
    public decimal TotalArrecadado { get; set; }
    public int ParcelasVencidas { get; set; }
    public decimal DividaAtiva { get; set; }
    public int DamsGerados { get; set; }
    public int CarnesEmitidos { get; set; }
}

public sealed class ContribuinteFormViewModel
{
    public long? Id { get; set; }
    [Required]
    [Display(Name = "Pessoa")]
    public long PessoaId { get; set; }
    [Required, StringLength(40)]
    [Display(Name = "Inscrição municipal")]
    public string InscricaoMunicipal { get; set; } = string.Empty;
    public bool Ativo { get; set; } = true;
    [StringLength(500)]
    public string? Observacoes { get; set; }
}

public sealed class CadastroImobiliarioFormViewModel
{
    public long? Id { get; set; }
    [Required, StringLength(60)]
    [Display(Name = "Inscrição imobiliária")]
    public string InscricaoImobiliaria { get; set; } = string.Empty;
    [Required]
    [Display(Name = "Contribuinte")]
    public long ContribuinteId { get; set; }
    [Required, StringLength(250)]
    public string Endereco { get; set; } = string.Empty;
    public decimal AreaTerreno { get; set; }
    public decimal AreaConstruida { get; set; }
}

public sealed class CadastroMercantilFormViewModel
{
    public long? Id { get; set; }
    [Required, StringLength(60)]
    [Display(Name = "Inscrição mercantil")]
    public string InscricaoMercantil { get; set; } = string.Empty;
    [Required]
    [Display(Name = "Contribuinte")]
    public long ContribuinteId { get; set; }
    [Required]
    [Display(Name = "Atividade econômica")]
    public long AtividadeEconomicaId { get; set; }
    public bool Ativo { get; set; } = true;
}

public sealed class AtividadeEconomicaFormViewModel
{
    public long? Id { get; set; }
    [Required, StringLength(20)]
    public string Codigo { get; set; } = string.Empty;
    [Required, StringLength(250)]
    public string Nome { get; set; } = string.Empty;
}

public sealed class LancamentoTributarioFormViewModel
{
    public long? Id { get; set; }
    [Required]
    public long ContribuinteId { get; set; }
    [Required, StringLength(80)]
    public string Tributo { get; set; } = string.Empty;
    [Required]
    public DateOnly DataLancamento { get; set; } = DateOnly.FromDateTime(DateTime.Today);
    [Range(typeof(decimal), "0.01", "79228162514264337593543950335")]
    public decimal ValorTotal { get; set; }
    [Range(1, 120)]
    public int QuantidadeParcelas { get; set; } = 1;
    [StringLength(500)]
    public string? Observacoes { get; set; }
}

public sealed class CertidaoFormViewModel
{
    [Required]
    public long ContribuinteId { get; set; }
    [Required]
    public string Tipo { get; set; } = "NEGATIVA";
}

public sealed class DividaAtivaFormViewModel
{
    [Required]
    public long ParcelaId { get; set; }
    [Required, StringLength(500)]
    public string Justificativa { get; set; } = string.Empty;
}

public sealed class CarneFormViewModel
{
    [Required, StringLength(120)]
    public string Descricao { get; set; } = string.Empty;
    [Range(1, int.MaxValue)]
    public int QuantidadeItens { get; set; } = 1;
}
