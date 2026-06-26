namespace Sigov.Web.Models.PostBuild;

public enum SigovFeatureStatus
{
    Funcional,
    Parcial,
    Demonstrativo,
    EmImplantacao,
    Indisponivel
}

public static class SigovFeatureStatusExtensions
{
    public static string ToDisplayName(this SigovFeatureStatus status) => status switch
    {
        SigovFeatureStatus.Funcional => "Funcional",
        SigovFeatureStatus.Parcial => "Parcial",
        SigovFeatureStatus.Demonstrativo => "Demonstrativo",
        SigovFeatureStatus.EmImplantacao => "Em implantação",
        SigovFeatureStatus.Indisponivel => "Indisponível",
        _ => "Indisponível"
    };

    public static string ToCssClass(this SigovFeatureStatus status) => status switch
    {
        SigovFeatureStatus.Funcional => "success",
        SigovFeatureStatus.Parcial => "warning",
        SigovFeatureStatus.Demonstrativo => "info",
        SigovFeatureStatus.EmImplantacao => "secondary",
        SigovFeatureStatus.Indisponivel => "danger",
        _ => "secondary"
    };
}

public sealed record DashboardCard(string Titulo, string Valor, string Descricao, string CssClass);
public sealed record ModuleViewModel(string Codigo, string Nome, SigovFeatureStatus Status, string Descricao)
{
    public string StatusDescricao => Status.ToDisplayName();
    public string StatusCssClass => Status.ToCssClass();
    public bool PodePersistirStatus => Status is SigovFeatureStatus.Funcional or SigovFeatureStatus.Parcial;
}
public sealed record HealthItemViewModel(string Nome, string Status, string Detalhe, bool Online);
public sealed record TenantListItemViewModel(long Id, string Nome, string Codigo, string Documento, string Email, string Telefone, string Plano, bool Ativo);

public sealed class TenantFormViewModel
{
    public long? Id { get; set; }
    public string Nome { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string? Documento { get; set; }
    public string? Email { get; set; }
    public string? Telefone { get; set; }
    public string? Plano { get; set; }
    public bool Ativo { get; set; } = true;
    public string? Observacao { get; set; }
    public string? CorPrincipal { get; set; }
    public string? LogoUrl { get; set; }
    public string? Subdominio { get; set; }
    public string? EmailSuporte { get; set; }
}


public sealed class DashboardViewModel
{
    public IReadOnlyCollection<DashboardCard> Cards { get; init; } = Array.Empty<DashboardCard>();
    public IReadOnlyCollection<HealthItemViewModel> Ambiente { get; init; } = Array.Empty<HealthItemViewModel>();
    public IReadOnlyCollection<ModuleViewModel> Modulos { get; init; } = Array.Empty<ModuleViewModel>();
    public string MensagemFallback { get; init; } = string.Empty;
}

public sealed class TenantsViewModel
{
    public IReadOnlyCollection<TenantListItemViewModel> Tenants { get; init; } = Array.Empty<TenantListItemViewModel>();
    public string Busca { get; init; } = string.Empty;
    public string MensagemFallback { get; init; } = string.Empty;
}

public sealed class ModulosSaasViewModel
{
    public long TenantId { get; init; }
    public IReadOnlyCollection<ModuleViewModel> Modulos { get; init; } = Array.Empty<ModuleViewModel>();
    public string MensagemFallback { get; init; } = string.Empty;
}

public sealed class HealthVisualViewModel
{
    public IReadOnlyCollection<HealthItemViewModel> Itens { get; init; } = Array.Empty<HealthItemViewModel>();
    public string Ambiente { get; init; } = string.Empty;
    public string Versao { get; init; } = string.Empty;
    public DateTimeOffset ServerTime { get; init; } = DateTimeOffset.UtcNow;
    public string MensagemFallback { get; init; } = string.Empty;
}

public sealed class MinhaCentralViewModel
{
    public string Perfil { get; init; } = "Operador";
    public string Tenant { get; init; } = "Ambiente demonstração";
    public IReadOnlyCollection<AcaoRecomendadaViewModel> Acoes { get; init; } = Array.Empty<AcaoRecomendadaViewModel>();
    public IReadOnlyCollection<ModuloResumoViewModel> Modulos { get; init; } = Array.Empty<ModuloResumoViewModel>();
    public IReadOnlyCollection<PendenciaViewModel> Pendencias { get; init; } = Array.Empty<PendenciaViewModel>();
    public IReadOnlyCollection<AlertaLgpdViewModel> AlertasLgpd { get; init; } = Array.Empty<AlertaLgpdViewModel>();
    public IReadOnlyCollection<AtividadeRecenteViewModel> Atividades { get; init; } = Array.Empty<AtividadeRecenteViewModel>();
    public IReadOnlyCollection<HealthItemViewModel> Ambiente { get; init; } = Array.Empty<HealthItemViewModel>();
    public string MensagemFallback { get; init; } = string.Empty;
}

public sealed record AcaoRecomendadaViewModel(string Titulo, string Descricao, string Url, string CssClass);
public sealed record ModuloResumoViewModel(string Codigo, string Nome, string Status);
public sealed record PendenciaViewModel(string Titulo, string Descricao, string Url);
public sealed record AlertaLgpdViewModel(string Titulo, string Descricao);
public sealed record AtividadeRecenteViewModel(string Acao, string Entidade, DateTimeOffset? Data);
