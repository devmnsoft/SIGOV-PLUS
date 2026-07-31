using System.Net;
using System.Text.RegularExpressions;

namespace Sigov.Application.Commercial;

public interface IDocumentTemplateRenderer
{
    string Render(string template, IReadOnlyDictionary<string, string?> values);
}

public sealed partial class SafeDocumentTemplateRenderer : IDocumentTemplateRenderer
{
    private static readonly HashSet<string> AllowedPlaceholders = new(StringComparer.Ordinal)
    {
        "empresa.nome", "empresa.documento", "empresa.email", "empresa.telefone",
        "cliente.nome", "cliente.documento", "cliente.email", "cliente.telefone",
        "orcamento.numero", "orcamento.validade", "orcamento.subtotal", "orcamento.desconto", "orcamento.total",
        "pedido.numero", "pedido.status", "pedido.data", "pedido.total", "itens.tabela"
    };

    public string Render(string template, IReadOnlyDictionary<string, string?> values)
    {
        ArgumentNullException.ThrowIfNull(template);
        ArgumentNullException.ThrowIfNull(values);
        if (string.IsNullOrWhiteSpace(template))
            throw new ArgumentException("O template deve possuir conteúdo.", nameof(template));
        RejectExecutableContent(template);

        var rendered = PlaceholderRegex().Replace(template, match =>
        {
            var key = match.Groups[1].Value;
            if (!AllowedPlaceholders.Contains(key)) throw new ArgumentException($"Placeholder não permitido: {key}.", nameof(template));
            if (!values.TryGetValue(key, out var value)) return string.Empty;
            return key == "itens.tabela"
                ? SanitizeItemsTable(value)
                : WebUtility.HtmlEncode(value ?? string.Empty) ?? string.Empty;
        });

        if (rendered is null)
            throw new InvalidOperationException("Não foi possível renderizar o template informado.");

        return rendered;
    }

    private static void RejectExecutableContent(string template)
    {
        if (ExecutableContentRegex().IsMatch(template)) throw new ArgumentException("O template contém conteúdo executável não permitido.", nameof(template));
    }

    private static string SanitizeItemsTable(string? html)
    {
        if (string.IsNullOrWhiteSpace(html)) return string.Empty;
        RejectExecutableContent(html);
        return DisallowedTagRegex().Replace(html, string.Empty);
    }

    [GeneratedRegex(@"\{\{\s*([a-z]+\.[a-z]+)\s*\}\}", RegexOptions.CultureInvariant)]
    private static partial Regex PlaceholderRegex();

    [GeneratedRegex(@"<(script|iframe|object|embed|link|meta)\b|\bon[a-z]+\s*=|javascript\s*:", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex ExecutableContentRegex();

    [GeneratedRegex(@"</?(?!table\b|thead\b|tbody\b|tfoot\b|tr\b|th\b|td\b)[a-z][^>]*>", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex DisallowedTagRegex();
}
