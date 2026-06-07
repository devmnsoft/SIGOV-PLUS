namespace Sigov.Application.Core.Pessoas;

public sealed class PessoaExportService
{
    private static readonly string[] SupportedFormats = { "csv", "json", "xml" };

    public bool IsSupported(string formato) => SupportedFormats.Contains(formato, StringComparer.OrdinalIgnoreCase);
}
