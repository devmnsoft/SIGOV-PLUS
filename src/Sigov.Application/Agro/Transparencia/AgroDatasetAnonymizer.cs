using System.Text.RegularExpressions;

namespace Sigov.Application.Agro.Transparencia;

public sealed class AgroDatasetAnonymizer
{
    private static readonly Regex EmailRegex = new("[A-Z0-9._%+-]+@[A-Z0-9.-]+\\.[A-Z]{2,}", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex DocumentoRegex = new("\\b\\d{3}\\.?\\d{3}\\.?\\d{3}-?\\d{2}\\b|\\b\\d{2}\\.?\\d{3}\\.?\\d{3}/?\\d{4}-?\\d{2}\\b", RegexOptions.Compiled);
    public string Anonymize(string content) => DocumentoRegex.Replace(EmailRegex.Replace(content, "***@***"), "***");
}
