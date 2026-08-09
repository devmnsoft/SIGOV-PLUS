namespace Sigov.Infrastructure.Persistence.Migrations;

internal sealed class MigrationTransactionException : InvalidOperationException
{
    public MigrationTransactionException(string message) : base(message)
    {
    }
}

internal static class MigrationSqlPolicy
{
    public static string PrepareForExecution(string version, string rawSql, bool legacyTransactionWrapper)
    {
        var statements = SplitTopLevelStatements(rawSql);
        var controls = statements.Where(statement => IsTransactionControl(statement.Text)).ToArray();

        if (!legacyTransactionWrapper)
        {
            if (controls.Length > 0)
            {
                throw new MigrationTransactionException(
                    $"Migration {version} contém controle transacional explícito não suportado pelo runner. " +
                    "Remova BEGIN/COMMIT/ROLLBACK/SAVEPOINT; a transação pertence ao MigrationRunner.");
            }

            return rawSql;
        }

        if (statements.Count < 3 || !IsBegin(statements[0].Text) || !IsCommit(statements[^1].Text) || controls.Length != 2)
        {
            throw new MigrationTransactionException(
                $"Migration legada {version} não possui exclusivamente um wrapper externo BEGIN/COMMIT reconhecível.");
        }

        // Preserve every byte between the wrapper statements. In particular, BEGIN/END in
        // dollar-quoted PL/pgSQL bodies are never tokenized as top-level statements.
        return rawSql[statements[0].End..statements[^1].Start];
    }

    private static bool IsBegin(string statement) => Normalize(statement) is "BEGIN" or "BEGIN TRANSACTION" or "BEGIN WORK";

    private static bool IsCommit(string statement) => Normalize(statement) is "COMMIT" or "COMMIT TRANSACTION" or "COMMIT WORK" or "END" or "END TRANSACTION" or "END WORK";

    private static bool IsTransactionControl(string statement)
    {
        var normalized = Normalize(statement);
        return normalized == "BEGIN" || normalized.StartsWith("BEGIN ", StringComparison.Ordinal) ||
            normalized.StartsWith("START TRANSACTION", StringComparison.Ordinal) ||
            normalized == "COMMIT" || normalized.StartsWith("COMMIT ", StringComparison.Ordinal) ||
            normalized == "END" || normalized.StartsWith("END ", StringComparison.Ordinal) || normalized.StartsWith("ROLLBACK", StringComparison.Ordinal) ||
            normalized.StartsWith("SAVEPOINT ", StringComparison.Ordinal) || normalized.StartsWith("RELEASE SAVEPOINT ", StringComparison.Ordinal) ||
            normalized.StartsWith("PREPARE TRANSACTION ", StringComparison.Ordinal);
    }

    private static string Normalize(string statement)
    {
        var words = new List<string>();
        var index = 0;
        while (index < statement.Length)
        {
            SkipTrivia(statement, ref index);
            var start = index;
            while (index < statement.Length && (char.IsLetter(statement[index]) || statement[index] == '_'))
            {
                index++;
            }

            if (start == index)
            {
                break;
            }

            words.Add(statement[start..index].ToUpperInvariant());
        }

        return string.Join(' ', words);
    }

    private static List<Statement> SplitTopLevelStatements(string sql)
    {
        var result = new List<Statement>();
        var statementStart = 0;
        var index = 0;
        while (index < sql.Length)
        {
            if (sql[index] == '\'' || sql[index] == '"')
            {
                var quote = sql[index];
                var backslashEscapes = quote == '\'' && index > 0 && (sql[index - 1] == 'e' || sql[index - 1] == 'E') &&
                    (index == 1 || !(char.IsLetterOrDigit(sql[index - 2]) || sql[index - 2] == '_'));
                SkipQuoted(sql, ref index, quote, backslashEscapes);
            }
            else if (sql[index] == '-' && index + 1 < sql.Length && sql[index + 1] == '-')
            {
                index += 2;
                while (index < sql.Length && sql[index] != '\n') index++;
            }
            else if (sql[index] == '/' && index + 1 < sql.Length && sql[index + 1] == '*')
            {
                SkipBlockComment(sql, ref index);
            }
            else if (sql[index] == '$' && TryReadDollarTag(sql, index, out var tag))
            {
                index += tag.Length;
                var close = sql.IndexOf(tag, index, StringComparison.Ordinal);
                if (close < 0) throw new MigrationTransactionException("Bloco dollar-quoted não terminado na migration.");
                index = close + tag.Length;
            }
            else if (sql[index] == ';')
            {
                var text = sql[statementStart..index];
                if (!string.IsNullOrWhiteSpace(RemoveLeadingTrivia(text)))
                {
                    result.Add(new Statement(statementStart, index + 1, text));
                }

                statementStart = ++index;
            }
            else
            {
                index++;
            }
        }

        var remainder = sql[statementStart..];
        if (!string.IsNullOrWhiteSpace(RemoveLeadingTrivia(remainder)))
        {
            result.Add(new Statement(statementStart, sql.Length, remainder));
        }

        return result;
    }

    private static string RemoveLeadingTrivia(string value)
    {
        var index = 0;
        SkipTrivia(value, ref index);
        return value[index..];
    }

    private static void SkipTrivia(string value, ref int index)
    {
        while (index < value.Length)
        {
            if (char.IsWhiteSpace(value[index]) || value[index] == '\uFEFF')
            {
                index++;
            }
            else if (value[index] == '-' && index + 1 < value.Length && value[index + 1] == '-')
            {
                index += 2;
                while (index < value.Length && value[index] != '\n') index++;
            }
            else if (value[index] == '/' && index + 1 < value.Length && value[index + 1] == '*')
            {
                SkipBlockComment(value, ref index);
            }
            else
            {
                break;
            }
        }
    }

    private static void SkipQuoted(string value, ref int index, char quote, bool backslashEscapes)
    {
        index++;
        while (index < value.Length)
        {
            if (backslashEscapes && value[index] == '\\' && index + 1 < value.Length)
            {
                index += 2;
                continue;
            }

            if (value[index++] != quote) continue;
            if (index < value.Length && value[index] == quote) { index++; continue; }
            return;
        }

        throw new MigrationTransactionException("Literal SQL não terminado na migration.");
    }

    private static void SkipBlockComment(string value, ref int index)
    {
        var depth = 1;
        index += 2;
        while (index < value.Length && depth > 0)
        {
            if (index + 1 < value.Length && value[index] == '/' && value[index + 1] == '*') { depth++; index += 2; }
            else if (index + 1 < value.Length && value[index] == '*' && value[index + 1] == '/') { depth--; index += 2; }
            else index++;
        }

        if (depth != 0) throw new MigrationTransactionException("Comentário SQL não terminado na migration.");
    }

    private static bool TryReadDollarTag(string value, int index, out string tag)
    {
        var end = index + 1;
        while (end < value.Length && (char.IsLetterOrDigit(value[end]) || value[end] == '_')) end++;
        if (end < value.Length && value[end] == '$')
        {
            tag = value[index..(end + 1)];
            return true;
        }

        tag = string.Empty;
        return false;
    }

    private sealed record Statement(int Start, int End, string Text);
}
