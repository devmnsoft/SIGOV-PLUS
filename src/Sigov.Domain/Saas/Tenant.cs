using Sigov.Domain.Common;

namespace Sigov.Domain.Saas;

public sealed class Tenant : AggregateRoot
{
    public Tenant(long id, string nome, TenantSlug slug, StatusTenant status, string timezone = "America/Sao_Paulo", string locale = "pt-BR")
    {
        Id = id;
        Nome = nome;
        Slug = slug;
        Status = status;
        Timezone = timezone;
        Locale = locale;
    }

    public long TenantId => Id;
    public string Nome { get; private set; }
    public TenantSlug Slug { get; private set; }
    public StatusTenant Status { get; private set; }
    public string Timezone { get; private set; }
    public string Locale { get; private set; }
    public bool PermiteOperacao => Status is StatusTenant.Ativo or StatusTenant.Implantacao or StatusTenant.Homologacao;

    public void Suspender() => Status = StatusTenant.Suspenso;

    public void Reativar() => Status = StatusTenant.Ativo;

    public void Cancelar() => Status = StatusTenant.Cancelado;
}
