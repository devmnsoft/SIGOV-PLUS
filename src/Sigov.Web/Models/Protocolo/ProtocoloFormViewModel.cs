using System.ComponentModel.DataAnnotations;

namespace Sigov.Web.Models.Protocolo;

public sealed class ProtocoloFormViewModel
{
    [Required(ErrorMessage = "Informe o assunto.")]
    [StringLength(180, MinimumLength = 5, ErrorMessage = "O assunto deve ter entre 5 e 180 caracteres.")]
    public string Assunto { get; set; } = string.Empty;

    [Required(ErrorMessage = "Informe o interessado.")]
    [StringLength(140)]
    public string Interessado { get; set; } = string.Empty;

    [StringLength(30)]
    [Display(Name = "Documento do interessado")]
    public string? Documento { get; set; }

    [Required, StringLength(80)] public string Categoria { get; set; } = "Solicitação";
    [Required] public string Prioridade { get; set; } = "NORMAL";

    [Required(ErrorMessage = "Informe a unidade de destino.")]
    [StringLength(120)]
    [Display(Name = "Unidade/setor de destino")]
    public string UnidadeDestino { get; set; } = string.Empty;

    [StringLength(2000)] public string? Observacao { get; set; }
}

public sealed class ProtocoloTarefaFormViewModel
{
    [Required, StringLength(220, MinimumLength = 5)]
    public string Titulo { get; set; } = string.Empty;

    [StringLength(2000)]
    public string? Descricao { get; set; }

    [Required]
    [Display(Name = "Responsável")]
    [Range(1, long.MaxValue, ErrorMessage = "Informe um responsável válido.")]
    public long ResponsavelId { get; set; }

    [Required]
    public string Prioridade { get; set; } = "NORMAL";

    [Display(Name = "Prazo")]
    public DateTimeOffset? PrazoEm { get; set; }
}

public sealed class ProtocoloDocumentoFormViewModel
{
    [Required]
    [Display(Name = "Documento GED")]
    [Range(1, long.MaxValue, ErrorMessage = "Informe um documento válido.")]
    public long DocumentoId { get; set; }
}
