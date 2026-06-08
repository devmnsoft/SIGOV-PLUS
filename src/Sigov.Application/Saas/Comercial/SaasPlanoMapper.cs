namespace Sigov.Application.Saas.Comercial;

public sealed class SaasPlanoMapper
{
    public static int NormalizePage(int page) => page < 1 ? 1 : page;
    public static int NormalizePageSize(int pageSize) => pageSize is < 1 or > 100 ? 20 : pageSize;
}
