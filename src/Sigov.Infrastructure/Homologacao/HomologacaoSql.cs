namespace Sigov.Infrastructure.Homologacao;

public static class HomologacaoSql
{
    public const string EnsureTenant = "select 1 from sigov.tenant where slug = @TenantSlug limit 1;";
}
