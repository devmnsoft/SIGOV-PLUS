namespace Sigov.Application.Transversal;

public static class CsvValueSanitizer
{
    private static readonly char[] FormulaPrefixes = ['=', '+', '-', '@'];

    public static string Escape(string? value)
    {
        var safe = value ?? string.Empty;
        if (safe.Length > 0 && FormulaPrefixes.Contains(safe[0]))
        {
            safe = "'" + safe;
        }

        return $"\"{safe.Replace("\"", "\"\"")}\"";
    }
}
