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
    public IReadOnlyCollection<DashboardStatusSliceViewModel> ProtocolosPorStatus { get; init; } = Array.Empty<DashboardStatusSliceViewModel>();
    public IReadOnlyCollection<DashboardListItemViewModel> UltimosProtocolos { get; init; } = Array.Empty<DashboardListItemViewModel>();
    public IReadOnlyCollection<DashboardListItemViewModel> TarefasCriticas { get; init; } = Array.Empty<DashboardListItemViewModel>();
    public IReadOnlyCollection<DashboardListItemViewModel> DocumentosRecentes { get; init; } = Array.Empty<DashboardListItemViewModel>();
    public string AlertaOperacional { get; init; } = string.Empty;
    public string MensagemFallback { get; init; } = string.Empty;
}

public sealed record DashboardStatusSliceViewModel(string Status, long Total);
public sealed record DashboardListItemViewModel(string Titulo, string Descricao, string Status, string Url, DateTimeOffset? Data);

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

public sealed class ParametrosSaasViewModel
{
    public long TenantId { get; init; }
    public string Categoria { get; init; } = string.Empty;
    public string Escopo { get; init; } = string.Empty;
    public string Busca { get; init; } = string.Empty;
    public IReadOnlyCollection<ParametroSaasItemViewModel> Parametros { get; init; } = Array.Empty<ParametroSaasItemViewModel>();
    public string MensagemFallback { get; init; } = string.Empty;
    public bool PodePersistir { get; init; }
}

public sealed record ParametroSaasItemViewModel(long Id, string Chave, string Valor, string Tipo, string Descricao, bool Sensivel);

public sealed class ParametroSaasFormViewModel
{
    public long? Id { get; set; }
    public long TenantId { get; set; }
    public string Escopo { get; set; } = string.Empty;
    public string Chave { get; set; } = string.Empty;
    public string Valor { get; set; } = string.Empty;
    public string Tipo { get; set; } = "texto";
    public string? Descricao { get; set; }
    public bool Sensivel { get; set; }
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

public sealed record SaasPlanoViewModel(long Id, string Codigo, string Nome, string Descricao, decimal ValorMensal, decimal ValorAnual, int LimiteUsuarios, int LimiteStorageGb, int LimiteTenants, string Suporte, string ModulosInclusos, bool Ativo, bool Recomendado, int Ordem, bool Persistido);
public sealed class SaasPlanosViewModel
{
    public IReadOnlyCollection<SaasPlanoViewModel> Planos { get; init; } = Array.Empty<SaasPlanoViewModel>();
    public bool PodePersistir { get; init; }
    public string MensagemFallback { get; init; } = string.Empty;
}
public sealed class SaasPlanoFormViewModel
{
    public long? Id { get; set; }
    public string Codigo { get; set; } = string.Empty;
    public string Nome { get; set; } = string.Empty;
    public string Descricao { get; set; } = string.Empty;
    public decimal ValorMensal { get; set; }
    public decimal ValorAnual { get; set; }
    public int LimiteUsuarios { get; set; }
    public int LimiteStorageGb { get; set; }
    public int LimiteTenants { get; set; }
    public string Suporte { get; set; } = string.Empty;
    public string ModulosInclusos { get; set; } = string.Empty;
    public bool Ativo { get; set; } = true;
    public bool Recomendado { get; set; }
    public int Ordem { get; set; }
}
public sealed record SaasAssinaturaViewModel(long Id, long TenantId, string Tenant, long PlanoId, string Plano, string Status, DateTime? Inicio, DateTime? Fim, decimal Valor, string Ciclo, int LimiteUsuarios, int LimiteStorageGb, string Observacoes, string ModulosIncluidos, bool Persistida);
public sealed class SaasAssinaturasViewModel
{
    public IReadOnlyCollection<SaasAssinaturaViewModel> Assinaturas { get; init; } = Array.Empty<SaasAssinaturaViewModel>();
    public bool PodePersistir { get; init; }
    public string MensagemFallback { get; init; } = string.Empty;
}
public sealed record SaasNotificationViewModel(long Id, string Tipo, string Titulo, string Descricao, string Status, DateTimeOffset? Data, bool Persistida);
public sealed class SaasNotificationsViewModel
{
    public IReadOnlyCollection<SaasNotificationViewModel> Notificacoes { get; init; } = Array.Empty<SaasNotificationViewModel>();
    public bool PodeMarcarLida { get; init; }
    public string StatusFiltro { get; init; } = string.Empty;
    public string MensagemFallback { get; init; } = string.Empty;
}
public sealed record GlobalSearchResultViewModel(string Area, string Titulo, string Descricao, string Url, string Badge, string Status = "", DateTimeOffset? Data = null, bool LgpdMascarado = false);
public sealed class GlobalSearchViewModel
{
    public string Query { get; init; } = string.Empty;
    public IReadOnlyCollection<GlobalSearchResultViewModel> Resultados { get; init; } = Array.Empty<GlobalSearchResultViewModel>();
    public IReadOnlyCollection<string> AreasIgnoradas { get; init; } = Array.Empty<string>();
    public string MensagemFallback { get; init; } = string.Empty;
}
