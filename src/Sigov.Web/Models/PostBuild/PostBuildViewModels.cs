namespace Sigov.Web.Models.PostBuild;

public sealed record DashboardCard(string Titulo, string Valor, string Descricao, string CssClass);
public sealed record ModuleViewModel(string Codigo, string Nome, string Status, string Descricao);
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
