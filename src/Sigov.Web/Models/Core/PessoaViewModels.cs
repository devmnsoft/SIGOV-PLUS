using System.ComponentModel.DataAnnotations;

namespace Sigov.Web.Models.Core;

public sealed class PessoaFormViewModel
{
    public long? Id { get; set; }

    [Display(Name = "Tipo")]
    public string TipoPessoa { get; set; } = "F";

    [Required]
    [StringLength(250)]
    public string Nome { get; set; } = string.Empty;

    [Display(Name = "Nome social")]
    [StringLength(250)]
    public string? NomeSocial { get; set; }

    [Display(Name = "CPF/CNPJ")]
    [StringLength(20)]
    public string? Documento { get; set; }

    [Display(Name = "E-mail")]
    [EmailAddress]
    [StringLength(250)]
    public string? Email { get; set; }

    [Display(Name = "Telefone")]
    [StringLength(30)]
    public string? Telefone { get; set; }

    [Display(Name = "Classificação LGPD")]
    [StringLength(40)]
    public string ClassificacaoLgpd { get; set; } = "Pessoal";

    [StringLength(500)]
    public string? Observacao { get; set; }

    public bool Ativo { get; set; } = true;
}

public sealed class EnderecoFormViewModel
{
    [Required]
    [StringLength(250)]
    public string Logradouro { get; set; } = string.Empty;

    [StringLength(30)]
    public string? Numero { get; set; }

    [StringLength(100)]
    public string? Complemento { get; set; }

    [StringLength(120)]
    public string? Bairro { get; set; }

    [Required]
    [StringLength(120)]
    public string Municipio { get; set; } = string.Empty;

    [Required]
    [StringLength(2, MinimumLength = 2)]
    public string Uf { get; set; } = string.Empty;

    [StringLength(12)]
    public string? Cep { get; set; }

    [StringLength(500)]
    public string? Observacao { get; set; }
}
