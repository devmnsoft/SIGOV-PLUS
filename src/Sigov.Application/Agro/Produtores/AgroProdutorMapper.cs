namespace Sigov.Application.Agro.Produtores;

public sealed class AgroProdutorMapper { public string Mask(string? value) => LgpdMasker.Mask(value); }
public sealed class AgroProdutorExportService { public byte[] ExportarCsv(IEnumerable<AgroProdutorResponse> rows) => System.Text.Encoding.UTF8.GetBytes(string.Join("\n", rows.Select(r => $"{r.CodigoProdutor};{r.NomePessoa};{r.Situacao}"))); }
internal static class LgpdMasker
{
    public static string? Mask(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return value;
        var digits = new string(value.Where(char.IsDigit).ToArray());
        if (digits.Length <= 4) return "***";
        return string.Concat(new string('*', Math.Max(0, digits.Length - 4)), digits[^4..]);
    }
}
