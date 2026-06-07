using System.ComponentModel.DataAnnotations;

namespace Sigov.Web.Models.Lote1;

public sealed class PessoaFiltroViewModel
{
    public string? Termo { get; set; }
    public string? TipoPessoa { get; set; }
    public bool? Ativo { get; set; }
    public string? ClassificacaoLgpd { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}

public sealed class PessoaDetalheViewModel
{
    public long Id { get; set; }
    public string Nome { get; set; } = string.Empty;
    public string DocumentoMascarado { get; set; } = string.Empty;
    public string ClassificacaoLgpd { get; set; } = "Pessoal";
}

public sealed class ContatoFormViewModel
{
    [Required]
    [StringLength(40)]
    public string Tipo { get; set; } = "E-mail";

    [Required]
    [StringLength(250)]
    public string Valor { get; set; } = string.Empty;

    public bool Principal { get; set; }
}

public sealed class UsuarioFormViewModel
{
    public long? Id { get; set; }

    [Required]
    [StringLength(120)]
    public string Login { get; set; } = string.Empty;

    [Required]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;

    public long? PessoaId { get; set; }
    public bool Ativo { get; set; } = true;
    public bool Bloqueado { get; set; }
    public bool DeveAlterarSenha { get; set; } = true;
    public bool MfaHabilitado { get; set; }
}

public sealed class UsuarioFiltroViewModel
{
    public string? Termo { get; set; }
    public bool? Ativo { get; set; }
    public bool? Bloqueado { get; set; }
}

public sealed class PerfilFormViewModel
{
    [Required]
    [StringLength(80)]
    public string Codigo { get; set; } = string.Empty;

    [Required]
    [StringLength(160)]
    public string Nome { get; set; } = string.Empty;

    public string? Descricao { get; set; }
}

public sealed class GrupoFormViewModel
{
    [Required]
    [StringLength(80)]
    public string Codigo { get; set; } = string.Empty;

    [Required]
    [StringLength(160)]
    public string Nome { get; set; } = string.Empty;
}

public sealed class PermissaoMatrixViewModel
{
    public string Modulo { get; set; } = string.Empty;
    public IReadOnlyList<string> Acoes { get; set; } = Array.Empty<string>();
}
