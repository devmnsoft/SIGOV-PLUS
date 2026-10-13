namespace Sigov.Web.Models;

// Ciclo 2 Sprint 6 (BR-6.1 a BR-6.6): suporte para a config das telas reais da Educação.
// Ações usam uma estrutura uniforme (mesmo tipo em todos os elementos do array) para que o
// array implícito do Razor compile; bodies são dicionários e nulls são nulos explícitos.
public sealed record EduActionConfirm(string Title, string Message, string Variant);

public static class EduArea
{
    public static Dictionary<string, object?> Body(params (string Key, object? Value)[] kv) =>
        kv.ToDictionary(x => x.Key, x => x.Value);

    public static Dictionary<string, object> PanelField(string label, string path, string? zeroNote = null)
    {
        var d = new Dictionary<string, object> { ["label"] = label, ["path"] = path };
        if (zeroNote != null) d["zeroNote"] = zeroNote;
        return d;
    }
}
