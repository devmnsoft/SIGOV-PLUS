namespace Sigov.Web.RazorCompat;

public static class municipio
{
    public static MunicipioGov gov { get; } = new();

    public sealed class MunicipioGov
    {
        public string br => string.Empty;
    }
}
