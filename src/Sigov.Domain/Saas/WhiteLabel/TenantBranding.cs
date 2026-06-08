using System.Text.RegularExpressions;
using Sigov.Domain.Common;

namespace Sigov.Domain.Saas.WhiteLabel;

public sealed class TenantBranding : Entity
{
    private static readonly Regex HexColorRegex = new("^#[0-9a-fA-F]{6}$", RegexOptions.Compiled, TimeSpan.FromMilliseconds(200));

    public TenantBranding(long id, long tenantId, string nomeExibicao, bool whiteLabelAtivo, WhiteLabelTema tema, string? cssCustomizado)
    {
        Id = id;
        TenantId = tenantId;
        NomeExibicao = nomeExibicao?.Trim() ?? string.Empty;
        WhiteLabelAtivo = whiteLabelAtivo;
        Tema = tema;
        CssCustomizado = SanitizeCss(cssCustomizado);
    }

    public long TenantId { get; }
    public string NomeExibicao { get; }
    public bool WhiteLabelAtivo { get; }
    public WhiteLabelTema Tema { get; }
    public string? CssCustomizado { get; }

    public Result Validate(bool planoOuAddonPermiteWhiteLabel)
    {
        if (TenantId <= 0) return Result.Failure("Branding exige tenant.");
        if (string.IsNullOrWhiteSpace(NomeExibicao)) return Result.Failure("Nome de exibição é obrigatório.");
        if (WhiteLabelAtivo && !planoOuAddonPermiteWhiteLabel) return Result.Failure("Plano ou addon não permite white label.");
        return Result.Success();
    }

    public static bool IsValidColor(string? color) => string.IsNullOrWhiteSpace(color) || HexColorRegex.IsMatch(color);

    public static string? SanitizeCss(string? css)
    {
        if (string.IsNullOrWhiteSpace(css)) return null;
        var limited = css.Length > 4000 ? css[..4000] : css;
        return limited.Replace("<", string.Empty, StringComparison.Ordinal).Replace(">", string.Empty, StringComparison.Ordinal);
    }
}
