using System.Data;
using System.Data.Common;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;
using DbMigration.Core.Sql;

namespace DbMigration.Core.Providers;

public abstract class DatabaseProviderBase : IDatabaseProvider
{
    public abstract DatabaseEngine Engine { get; }

    public abstract string DisplayName { get; }

    public virtual int? DefaultPort => null;

    public virtual bool IsFileBased => false;

    protected abstract char IdentifierOpenQuote { get; }

    protected virtual char IdentifierCloseQuote => IdentifierOpenQuote;

    protected virtual char IdentifierEscapeChar => IdentifierOpenQuote;

    protected virtual bool SupportsIfNotExists => true;

    protected virtual bool UseInlinePrimaryKey => false;

    protected virtual string TestQuery => "SELECT 1;";

    public abstract string BuildConnectionString(ConnectionProfile profile, bool revealPassword);

    public abstract DbConnection CreateConnection(ConnectionProfile profile);

    public abstract string MapToNativeType(CanonicalType type, int? maxLength);

    public virtual string QuoteIdentifier(string identifier)
    {
        ArgumentNullException.ThrowIfNull(identifier);
        var escaped = identifier.Replace(
            IdentifierEscapeChar.ToString(),
            new string(IdentifierEscapeChar, 2),
            StringComparison.Ordinal);
        return string.Concat(IdentifierOpenQuote, escaped, IdentifierCloseQuote);
    }

    public virtual string BuildInsertSql(string table, IReadOnlyList<string> columns)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(table);
        ArgumentNullException.ThrowIfNull(columns);
        if (columns.Count == 0)
        {
            throw new ArgumentException("At least one column is required.", nameof(columns));
        }

        var builder = new StringBuilder();
        builder.Append("INSERT INTO ").Append(this.QuoteQualifiedName(table)).Append(" (");
        for (var i = 0; i < columns.Count; i++)
        {
            if (i > 0)
            {
                builder.Append(", ");
            }

            builder.Append(QuoteIdentifier(columns[i]));
        }

        builder.Append(") VALUES (");
        for (var i = 0; i < columns.Count; i++)
        {
            if (i > 0)
            {
                builder.Append(", ");
            }

            builder.Append("@p").Append(i.ToString(CultureInfo.InvariantCulture));
        }

        builder.Append(");");
        return builder.ToString();
    }

    public string BuildCreateTableSql(TableSchema table, bool ifNotExists)
    {
        ArgumentNullException.ThrowIfNull(table);

        var primaryKey = table.PrimaryKey;
        var inlinePrimaryKey = UseInlinePrimaryKey && primaryKey.Count == 1 && primaryKey[0].IsAutoIncrement
            ? primaryKey[0]
            : null;

        var definitions = new List<string>(table.Columns.Count + 1);
        foreach (var column in table.Columns.OrderBy(static c => c.Ordinal))
        {
            definitions.Add(BuildColumnDefinition(column, ReferenceEquals(column, inlinePrimaryKey)));
        }

        if (primaryKey.Count > 0 && inlinePrimaryKey is null)
        {
            definitions.Add(BuildPrimaryKeyConstraint(primaryKey));
        }

        var builder = new StringBuilder();
        builder.Append("CREATE TABLE ");
        if (ifNotExists && SupportsIfNotExists)
        {
            builder.Append("IF NOT EXISTS ");
        }

        builder.Append(this.QuoteQualifiedName(table.QualifiedName)).AppendLine(" (");
        builder.Append(string.Join(",\n", definitions));
        builder.AppendLine();
        builder.Append(");");

        var sql = builder.ToString();
        return ifNotExists && !SupportsIfNotExists ? WrapCreateTableIfNotExists(sql, table) : sql;
    }

    public async Task<string> TestConnectionAsync(ConnectionProfile profile, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(profile);

        await using var connection = CreateConnection(profile);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        var description = await DescribeServerAsync(connection, cancellationToken).ConfigureAwait(false);
        if (!string.IsNullOrWhiteSpace(description))
        {
            return description;
        }

        await using var command = connection.CreateCommand();
        command.CommandText = TestQuery;
        await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        return $"{DisplayName} {connection.ServerVersion}";
    }

    public abstract Task<IReadOnlyList<TableSchema>> GetSchemaAsync(
        ConnectionProfile profile,
        CancellationToken cancellationToken = default);

    public async Task<QueryResult> ExecuteQueryAsync(
        ConnectionProfile profile,
        string sql,
        int maxRows,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(sql);

        var stopwatch = Stopwatch.StartNew();
        var columns = new List<string>();
        var rows = new List<object?[]>();
        var hasRows = false;
        var truncated = false;
        int? recordsAffected = null;

        await using var connection = CreateConnection(profile);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);

        do
        {
            if (reader.FieldCount <= 0)
            {
                continue;
            }

            columns.Clear();
            for (var i = 0; i < reader.FieldCount; i++)
            {
                columns.Add(reader.GetName(i));
            }

            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                if (maxRows > 0 && rows.Count >= maxRows)
                {
                    truncated = true;
                    break;
                }

                var values = new object?[reader.FieldCount];
                for (var i = 0; i < reader.FieldCount; i++)
                {
                    var value = reader.GetValue(i);
                    values[i] = value is DBNull ? null : value;
                }

                rows.Add(values);
            }

            hasRows = true;
            break;
        }
        while (await reader.NextResultAsync(cancellationToken).ConfigureAwait(false));

        var affected = reader.RecordsAffected;
        if (affected >= 0)
        {
            recordsAffected = affected;
        }

        stopwatch.Stop();
        return new QueryResult
        {
            Columns = columns,
            Rows = rows,
            RecordsAffected = recordsAffected,
            HasRows = hasRows,
            Truncated = truncated,
            Elapsed = stopwatch.Elapsed,
        };
    }

    public async Task<int> ExecuteNonQueryAsync(
        ConnectionProfile profile,
        string sql,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(sql);

        await using var connection = CreateConnection(profile);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        return await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    public async IAsyncEnumerable<object?[]> ReadTableAsync(
        ConnectionProfile profile,
        string table,
        IReadOnlyList<ColumnSchema> columns,
        int batchSize,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentException.ThrowIfNullOrWhiteSpace(table);
        ArgumentNullException.ThrowIfNull(columns);

        await using var connection = CreateConnection(profile);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = BuildSelectSql(table, columns);
        await using var reader = await command
            .ExecuteReaderAsync(CommandBehavior.SequentialAccess, cancellationToken)
            .ConfigureAwait(false);

        var fieldCount = reader.FieldCount;
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            var values = new object?[fieldCount];
            for (var i = 0; i < fieldCount; i++)
            {
                var value = reader.GetValue(i);
                values[i] = value is DBNull ? null : value;
            }

            yield return values;
        }
    }

    public async Task<long?> GetRowCountAsync(
        ConnectionProfile profile,
        string table,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentException.ThrowIfNullOrWhiteSpace(table);

        try
        {
            await using var connection = CreateConnection(profile);
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT COUNT(*) FROM " + this.QuoteQualifiedName(table);
            var value = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
            return value is null || value is DBNull
                ? null
                : Convert.ToInt64(value, CultureInfo.InvariantCulture);
        }
        catch (DbException)
        {
            return null;
        }
        catch (InvalidOperationException)
        {
            return null;
        }
        catch (ArgumentException)
        {
            return null;
        }
    }

    protected abstract string BuildColumnDefinition(ColumnSchema column, bool inlinePrimaryKey);

    protected virtual string BuildPrimaryKeyConstraint(IReadOnlyList<ColumnSchema> primaryKey)
    {
        ArgumentNullException.ThrowIfNull(primaryKey);
        var builder = new StringBuilder("PRIMARY KEY (");
        for (var i = 0; i < primaryKey.Count; i++)
        {
            if (i > 0)
            {
                builder.Append(", ");
            }

            builder.Append(QuoteIdentifier(primaryKey[i].Name));
        }

        builder.Append(')');
        return builder.ToString();
    }

    protected virtual string WrapCreateTableIfNotExists(string createSql, TableSchema table) => createSql;

    protected string? FormatDefaultValue(ColumnSchema column)
    {
        ArgumentNullException.ThrowIfNull(column);
        var raw = column.DefaultValue;
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        var value = raw.Trim();
        if (IsDefaultExpression(value))
        {
            return value;
        }

        return SqlLiteralFormatter.Format(ConvertFromDefaultText(value, column.Type), Engine);
    }

    protected string BuildSelectSql(string table, IReadOnlyList<ColumnSchema> columns)
    {
        var builder = new StringBuilder("SELECT ");
        if (columns.Count == 0)
        {
            builder.Append('*');
        }
        else
        {
            for (var i = 0; i < columns.Count; i++)
            {
                if (i > 0)
                {
                    builder.Append(", ");
                }

                builder.Append(QuoteIdentifier(columns[i].Name));
            }
        }

        builder.Append(" FROM ").Append(this.QuoteQualifiedName(table)).Append(';');
        return builder.ToString();
    }

    protected virtual async Task<string?> DescribeServerAsync(DbConnection connection, CancellationToken cancellationToken)
    {
        await Task.CompletedTask.ConfigureAwait(false);
        return string.IsNullOrEmpty(connection.ServerVersion) ? null : $"{DisplayName} {connection.ServerVersion}";
    }

    protected static async Task<string?> ExecuteScalarStringAsync(
        DbConnection connection,
        string sql,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        var value = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        return value is null || value is DBNull ? null : Convert.ToString(value, CultureInfo.InvariantCulture);
    }

    private static object? ConvertFromDefaultText(string value, CanonicalType type)
    {
        var trimmed = value.Trim('\'', '"');
        return type switch
        {
            CanonicalType.Boolean => trimmed is "1" or "true" or "TRUE" or "t",
            CanonicalType.Integer or CanonicalType.BigInt => long.TryParse(trimmed, NumberStyles.Integer, CultureInfo.InvariantCulture, out var l) ? l : trimmed,
            CanonicalType.Real or CanonicalType.Decimal => decimal.TryParse(trimmed, NumberStyles.Float, CultureInfo.InvariantCulture, out var d) ? d : trimmed,
            _ => trimmed,
        };
    }

    private static bool IsDefaultExpression(string value)
    {
        if (value.Length == 0)
        {
            return false;
        }

        if (value[0] is '\'' or '"' or '(')
        {
            return true;
        }

        if (value.StartsWith("X'", StringComparison.OrdinalIgnoreCase) || value.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (decimal.TryParse(value, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out _))
        {
            return true;
        }

        return value.Contains('(', StringComparison.Ordinal)
            || value.Equals("NULL", StringComparison.OrdinalIgnoreCase)
            || value.Equals("CURRENT_TIMESTAMP", StringComparison.OrdinalIgnoreCase)
            || value.Equals("CURRENT_DATE", StringComparison.OrdinalIgnoreCase)
            || value.Equals("CURRENT_TIME", StringComparison.OrdinalIgnoreCase)
            || value.Equals("TRUE", StringComparison.OrdinalIgnoreCase)
            || value.Equals("FALSE", StringComparison.OrdinalIgnoreCase);
    }
}

