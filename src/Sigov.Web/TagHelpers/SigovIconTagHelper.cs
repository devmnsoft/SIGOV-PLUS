using Microsoft.AspNetCore.Razor.TagHelpers;
using Sigov.Web.Services.Visual;

namespace Sigov.Web.TagHelpers;

[HtmlTargetElement("sigov-icon")]
public sealed class SigovIconTagHelper(IIconRegistry registry) : TagHelper
{
    private static readonly HashSet<int> Sizes = [16, 18, 20, 24, 32];
    [HtmlAttributeName("name")] public required string Name { get; set; }
    [HtmlAttributeName("size")] public int Size { get; set; } = 20;
    [HtmlAttributeName("title")] public string? Title { get; set; }
    [HtmlAttributeName("decorative")] public bool Decorative { get; set; } = true;

    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        if (!registry.TryGet(Name, out var icon)) throw new InvalidOperationException($"Ícone SIGOV não registrado: {Name}");
        if (!Sizes.Contains(Size)) throw new InvalidOperationException($"Tamanho de ícone não canônico: {Size}");
        output.TagName = "svg";
        output.Attributes.SetAttribute("class", $"sigov-icon sigov-icon--{Size}");
        output.Attributes.SetAttribute("width", Size);
        output.Attributes.SetAttribute("height", Size);
        output.Attributes.SetAttribute("focusable", "false");
        if (Decorative) output.Attributes.SetAttribute("aria-hidden", "true");
        else { output.Attributes.SetAttribute("role", "img"); output.Attributes.SetAttribute("aria-label", Title ?? Name); }
        output.Content.SetHtmlContent($"<use href=\"/icons/sigov-icons.svg#{icon.SymbolId}\"></use>");
    }
}
