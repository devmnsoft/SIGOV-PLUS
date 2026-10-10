namespace Sigov.Application.Educacao.Avancada;

/// <summary>
/// Formatação de exportação CSV dos relatórios de educação avançada.
/// Usa delimitador ';' (convenção pt-BR do Excel), preserva delimitadores, aspas e
/// quebras de linha com escape correto (não remove separadores do conteúdo original)
/// e aplica proteção contra injeção de fórmula em planilhas.
/// </summary>
public static class EducacaoCsv
{
    /// <summary>Escapa um campo para CSV preservando o conteúdo e protegendo fórmulas.</summary>
    public static string Campo(string? valor)
    {
        if (string.IsNullOrEmpty(valor)) return string.Empty;

        var v = valor;
        // Célula que inicia com caractere que planilhas tratam como expressão
        // (=, +, -, @, tab, CR) recebe um apóstrofo à esquerda para virar texto puro.
        if (v[0] is '=' or '+' or '-' or '@' or '\t' or '\r') v = "'" + v;

        var precisaAspa = v.IndexOf(';') >= 0 || v.IndexOf('"') >= 0 || v.IndexOf('\n') >= 0 || v.IndexOf('\r') >= 0;
        if (precisaAspa) v = "\"" + v.Replace("\"", "\"\"", StringComparison.Ordinal) + "\"";
        return v;
    }
}
