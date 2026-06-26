using System.ComponentModel.DataAnnotations;

namespace Sigov.Web.Models.Lote1;

public sealed class UsuarioListItemViewModel
{
    public long Id { get; set; }
    public string Nome { get; set; } = string.Empty;
    public string Login { get; set; } = string.Empty;
    public string EmailMascarado { get; set; } = string.Empty;
    public string Tenant { get; set; } = string.Empty;
    public string Perfil { get; set; } = string.Empty;
    public bool Ativo { get; set; }
    public bool Bloqueado { get; set; }
    public string Status => Bloqueado ? "Bloqueado" : Ativo ? "Ativo" : "Inativo";
}

public sealed class UsuarioDetalheViewModel : UsuarioFormViewModel
{
    public string Nome { get; set; } = string.Empty;
    public string Tenant { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string MensagemFallback { get; set; } = string.Empty;
}

public sealed class UsuariosAdminViewModel
{
    public UsuarioFiltroViewModel Filtro { get; set; } = new();
    public UsuarioFormViewModel Form { get; set; } = new();
    public IReadOnlyCollection<UsuarioListItemViewModel> Usuarios { get; set; } = Array.Empty<UsuarioListItemViewModel>();
    public string MensagemFallback { get; set; } = string.Empty;
}

public sealed class PerfilListItemViewModel
{
    public long Id { get; set; }
    public string Codigo { get; set; } = string.Empty;
    public string Nome { get; set; } = string.Empty;
    public string Descricao { get; set; } = string.Empty;
    public bool Ativo { get; set; }
}

public sealed class PerfilDetalheViewModel : PerfilFormViewModel
{
    public long Id { get; set; }
    public bool Ativo { get; set; } = true;
    public IReadOnlyCollection<string> Permissoes { get; set; } = Array.Empty<string>();
    public string MensagemFallback { get; set; } = string.Empty;
}

public sealed class PerfisAdminViewModel
{
    public PerfilFormViewModel Form { get; set; } = new();
    public IReadOnlyCollection<PerfilListItemViewModel> Perfis { get; set; } = Array.Empty<PerfilListItemViewModel>();
    public string MensagemFallback { get; set; } = string.Empty;
}
