using System.Globalization;

namespace Sigov.Application.Rh;

public static class RhLgpdMaskingPolicy
{
    private static readonly string[] SensitiveKeys =
    {
        "dadosBancarios", "banco", "agencia", "conta", "contaBancaria", "pix",
        "resultadoExame", "resultado", "laudo", "cid", "motivoSensivel", "observacaoSaude"
    };

    public static Dictionary<string, object?> Mask(Dictionary<string, object?> dados)
    {
        var copy = new Dictionary<string, object?>(dados, StringComparer.OrdinalIgnoreCase);
        MaskDocumento(copy, "cpf", 3, 2);
        MaskDocumento(copy, "cnpj", 2, 2);
        MaskEmail(copy, "email");
        MaskEmail(copy, "emailInstitucional");
        MaskTelefone(copy, "telefone");
        foreach (var key in SensitiveKeys)
        {
            if (copy.ContainsKey(key)) copy[key] = "***";
        }

        copy["classificacaoLgpd"] = "dados_pessoais_sensiveis";
        return copy;
    }

    public static string? MaskCpf(string? cpf) => MaskText(cpf, 3, 2);
    public static string? MaskEmailValue(string? email)
    {
        if (string.IsNullOrWhiteSpace(email)) return email;
        var at = email.IndexOf('@', StringComparison.Ordinal);
        return at <= 1 ? "***" : email[0] + "***" + email[at..];
    }

    public static string? MaskTelefoneValue(string? telefone) => string.IsNullOrWhiteSpace(telefone) ? telefone : (telefone.Length <= 4 ? "***" : "***" + telefone[^4..]);

    private static string? MaskText(object? value, int visibleStart, int visibleEnd)
    {
        if (value is null) return null;
        var text = Convert.ToString(value, CultureInfo.InvariantCulture);
        if (string.IsNullOrWhiteSpace(text)) return text;
        return text.Length <= visibleStart + visibleEnd ? "***" : text[..visibleStart] + new string('*', text.Length - visibleStart - visibleEnd) + text[^visibleEnd..];
    }

    private static void MaskDocumento(IDictionary<string, object?> dados, string key, int visibleStart, int visibleEnd)
    {
        if (dados.ContainsKey(key)) dados[key] = MaskText(dados[key], visibleStart, visibleEnd);
    }

    private static void MaskEmail(IDictionary<string, object?> dados, string key)
    {
        if (dados.TryGetValue(key, out var value)) dados[key] = MaskEmailValue(Convert.ToString(value, CultureInfo.InvariantCulture));
    }

    private static void MaskTelefone(IDictionary<string, object?> dados, string key)
    {
        if (dados.TryGetValue(key, out var value)) dados[key] = MaskTelefoneValue(Convert.ToString(value, CultureInfo.InvariantCulture));
    }
}
