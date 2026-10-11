using System.Text;

namespace DbMigration.Core.Security;

public static class SqlGuard
{
    public static SqlGuardVerdict Evaluate(string sql)
    {
        if (string.IsNullOrWhiteSpace(sql))
        {
            return new SqlGuardVerdict(SqlGuardLevel.Safe, "Нечего проверять.");
        }

        var findings = new List<(SqlGuardLevel Level, string Message)>();
        foreach (var statement in SplitStatements(sql))
        {
            CollectFindings(statement, findings);
        }

        if (findings.Count == 0)
        {
            return new SqlGuardVerdict(SqlGuardLevel.Safe, "Опасных операторов не обнаружено.");
        }

        var worst = findings.MaxBy(static f => f.Level);
        var messages = findings
            .Where(f => f.Level == worst.Level)
            .Select(static f => f.Message)
            .Distinct(StringComparer.Ordinal);

        return new SqlGuardVerdict(worst.Level, string.Join(" ", messages));
    }

    private static void CollectFindings(string statement, List<(SqlGuardLevel Level, string Message)> findings)
    {
        var tokens = Tokenize(statement);
        if (tokens.Count == 0)
        {
            return;
        }

        var hasWhere = tokens.Contains("WHERE");

        if (tokens.Contains("DROP"))
        {
            findings.Add((SqlGuardLevel.Destructive, "DROP безвозвратно удаляет объект базы данных."));
        }

        if (tokens.Contains("TRUNCATE"))
        {
            findings.Add((SqlGuardLevel.Destructive, "TRUNCATE удаляет все строки таблицы."));
        }

        if (tokens.Contains("DELETE"))
        {
            findings.Add(hasWhere
                ? (SqlGuardLevel.Warning, "DELETE удаляет строки, попадающие под условие WHERE.")
                : (SqlGuardLevel.Destructive, "DELETE без WHERE удаляет все строки."));
        }

        if (tokens.Contains("UPDATE"))
        {
            findings.Add(hasWhere
                ? (SqlGuardLevel.Warning, "UPDATE изменяет строки, попадающие под условие WHERE.")
                : (SqlGuardLevel.Destructive, "UPDATE без WHERE изменяет все строки."));
        }

        if (tokens.Contains("ALTER"))
        {
            findings.Add((SqlGuardLevel.Warning, "ALTER изменяет структуру существующего объекта."));
        }

        if (tokens.Contains("GRANT") || tokens.Contains("REVOKE"))
        {
            findings.Add((SqlGuardLevel.Warning, "GRANT/REVOKE изменяет права доступа."));
        }

        if (tokens.Contains("EXEC") || tokens.Contains("EXECUTE") || tokens.Contains("CALL"))
        {
            findings.Add((SqlGuardLevel.Warning, "Выполняет хранимую процедуру или динамический SQL."));
        }

        if (tokens.Contains("MERGE"))
        {
            findings.Add((SqlGuardLevel.Warning, "MERGE может вставлять, изменять и удалять строки."));
        }
    }

    private static HashSet<string> Tokenize(string statement)
    {
        var tokens = new HashSet<string>(StringComparer.Ordinal);
        var index = 0;
        while (index < statement.Length)
        {
            if (char.IsLetter(statement[index]) || statement[index] == '_')
            {
                var start = index;
                while (index < statement.Length && (char.IsLetterOrDigit(statement[index]) || statement[index] == '_'))
                {
                    index++;
                }

                tokens.Add(statement[start..index].ToUpperInvariant());
            }
            else
            {
                index++;
            }
        }

        return tokens;
    }

    private static List<string> SplitStatements(string sql)
    {
        var statements = new List<string>();
        var current = new StringBuilder();
        var index = 0;
        var length = sql.Length;

        while (index < length)
        {
            var character = sql[index];

            if (character is '\'' or '"' or '`')
            {
                index = SkipQuoted(sql, index, character);
                continue;
            }

            if (character == '[')
            {
                index = SkipBracket(sql, index);
                continue;
            }

            if (character == '-' && index + 1 < length && sql[index + 1] == '-')
            {
                index += 2;
                while (index < length && sql[index] != '\n')
                {
                    index++;
                }

                continue;
            }

            if (character == '/' && index + 1 < length && sql[index + 1] == '*')
            {
                index += 2;
                while (index + 1 < length && !(sql[index] == '*' && sql[index + 1] == '/'))
                {
                    index++;
                }

                index = Math.Min(index + 2, length);
                continue;
            }

            if (character == ';')
            {
                statements.Add(current.ToString());
                current.Clear();
                index++;
                continue;
            }

            current.Append(character);
            index++;
        }

        if (current.Length > 0)
        {
            statements.Add(current.ToString());
        }

        return statements;
    }

    private static int SkipQuoted(string sql, int start, char quote)
    {
        var index = start + 1;
        while (index < sql.Length)
        {
            if (sql[index] == quote)
            {
                if (index + 1 < sql.Length && sql[index + 1] == quote)
                {
                    index += 2;
                    continue;
                }

                return index + 1;
            }

            index++;
        }

        return index;
    }

    private static int SkipBracket(string sql, int start)
    {
        var index = start + 1;
        while (index < sql.Length)
        {
            if (sql[index] == ']')
            {
                if (index + 1 < sql.Length && sql[index + 1] == ']')
                {
                    index += 2;
                    continue;
                }

                return index + 1;
            }

            index++;
        }

        return index;
    }
}

