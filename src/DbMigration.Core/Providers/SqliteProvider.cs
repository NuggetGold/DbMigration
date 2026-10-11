using System.Data.Common;
using System.Globalization;
using System.Text;
using Microsoft.Data.Sqlite;

namespace DbMigration.Core.Providers;

public sealed class SqliteProvider : DatabaseProviderBase
{
    public override DatabaseEngine Engine => DatabaseEngine.Sqlite;

    public override string DisplayName => "SQLite";

    public override bool IsFileBased => true;

    protected override char IdentifierOpenQuote => '"';

    protected override bool UseInlinePrimaryKey => true;

    public override string BuildConnectionString(ConnectionProfile profile, bool revealPassword)
    {
        ArgumentNullException.ThrowIfNull(profile);

        var path = string.IsNullOrWhiteSpace(profile.FilePath) ? ":memory:" : profile.FilePath;
        var builder = new SqliteConnectionStringBuilder
        {
            DataSource = path,
        };

        var baseString = builder.ToString();
        return string.IsNullOrWhiteSpace(profile.ExtraOptions)
            ? baseString
            : baseString + ";" + profile.ExtraOptions;
    }

    public override DbConnection CreateConnection(ConnectionProfile profile) =>
        new SqliteConnection(BuildConnectionString(profile, revealPassword: true));

    public override string MapToNativeType(CanonicalType type, int? maxLength) => type switch
    {
        CanonicalType.Boolean or CanonicalType.Integer or CanonicalType.BigInt => "INTEGER",
        CanonicalType.Real => "REAL",
        CanonicalType.Decimal => "NUMERIC",
        CanonicalType.Blob => "BLOB",
        _ => "TEXT",
    };

    public override async Task<IReadOnlyList<TableSchema>> GetSchemaAsync(
        ConnectionProfile profile,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(profile);

        await using var connection = CreateConnection(profile);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        var tables = await ReadTableDefinitionsAsync(connection, cancellationToken).ConfigureAwait(false);
        var result = new List<TableSchema>(tables.Count);

        foreach (var (name, createSql) in tables)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var columns = await ReadColumnsAsync(connection, name, createSql, cancellationToken).ConfigureAwait(false);
            var indexes = await ReadIndexesAsync(connection, name, cancellationToken).ConfigureAwait(false);
            var foreignKeys = await ReadForeignKeysAsync(connection, name, cancellationToken).ConfigureAwait(false);
            var rowCount = await GetRowCountAsync(profile, name, cancellationToken).ConfigureAwait(false);

            result.Add(new TableSchema(name, null, columns, indexes, foreignKeys, rowCount));
        }

        return result;
    }

    protected override string BuildColumnDefinition(ColumnSchema column, bool inlinePrimaryKey)
    {
        ArgumentNullException.ThrowIfNull(column);

        var builder = new StringBuilder();
        builder.Append(QuoteIdentifier(column.Name)).Append(' ').Append(MapToNativeType(column.Type, column.MaxLength));

        if (inlinePrimaryKey)
        {
            builder.Append(" PRIMARY KEY AUTOINCREMENT");
        }

        if (!column.IsNullable || column.IsPrimaryKey)
        {
            builder.Append(" NOT NULL");
        }

        var defaultValue = FormatDefaultValue(column);
        if (defaultValue is not null)
        {
            builder.Append(" DEFAULT ").Append(defaultValue);
        }

        return builder.ToString();
    }

    protected override async Task<string?> DescribeServerAsync(DbConnection connection, CancellationToken cancellationToken)
    {
        var version = await ExecuteScalarStringAsync(connection, "SELECT sqlite_version();", cancellationToken)
            .ConfigureAwait(false);
        return version is null ? "SQLite" : "SQLite " + version;
    }

    private static async Task<List<(string Name, string CreateSql)>> ReadTableDefinitionsAsync(
        DbConnection connection,
        CancellationToken cancellationToken)
    {
        var tables = new List<(string, string)>();
        await using var command = connection.CreateCommand();
        command.CommandText =
            "SELECT name, COALESCE(sql, '') FROM sqlite_master " +
            "WHERE type = 'table' AND name NOT LIKE 'sqlite_%' ORDER BY name;";
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            tables.Add((reader.GetString(0), reader.GetString(1)));
        }

        return tables;
    }

    private async Task<IReadOnlyList<ColumnSchema>> ReadColumnsAsync(
        DbConnection connection,
        string table,
        string createSql,
        CancellationToken cancellationToken)
    {
        var raw = new List<(string Name, string Type, bool NotNull, string? Default, int Pk)>();
        await using (var command = connection.CreateCommand())
        {
            command.CommandText = "PRAGMA table_info(" + QuoteIdentifier(table) + ");";
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                var name = reader.GetString(1);
                var type = reader.IsDBNull(2) ? string.Empty : reader.GetString(2);
                var notNull = reader.GetInt32(3) != 0;
                var defaultValue = reader.IsDBNull(4) ? null : reader.GetString(4);
                var pk = reader.GetInt32(5);
                raw.Add((name, type, notNull, defaultValue, pk));
            }
        }

        var primaryKeyCount = raw.Count(static c => c.Pk > 0);
        var hasAutoIncrementKeyword = createSql.Contains("AUTOINCREMENT", StringComparison.OrdinalIgnoreCase);
        var columns = new List<ColumnSchema>(raw.Count);

        for (var ordinal = 0; ordinal < raw.Count; ordinal++)
        {
            var (name, type, notNull, defaultValue, pk) = raw[ordinal];
            var isPrimaryKey = pk > 0;
            var affinityInteger = CanonicalTypeMapper.Infer(type) is CanonicalType.Integer or CanonicalType.BigInt;
            var isAutoIncrement = isPrimaryKey
                && affinityInteger
                && (primaryKeyCount == 1 || hasAutoIncrementKeyword);

            columns.Add(new ColumnSchema(
                name,
                type,
                CanonicalTypeMapper.Infer(type),
                !notNull && !isPrimaryKey,
                isPrimaryKey,
                isAutoIncrement,
                pk > 0 ? pk : ordinal + 1,
                defaultValue,
                CanonicalTypeMapper.ParseDeclaredLength(type)));
        }

        return columns;
    }

    private async Task<IReadOnlyList<IndexSchema>> ReadIndexesAsync(
        DbConnection connection,
        string table,
        CancellationToken cancellationToken)
    {
        var definitions = new List<(string Name, bool Unique, string Origin)>();
        await using (var command = connection.CreateCommand())
        {
            command.CommandText = "PRAGMA index_list(" + QuoteIdentifier(table) + ");";
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                var name = reader.IsDBNull(1) ? string.Empty : reader.GetString(1);
                var unique = reader.GetInt32(2) != 0;
                var origin = reader.IsDBNull(3) ? string.Empty : reader.GetString(3);
                if (!string.IsNullOrEmpty(name))
                {
                    definitions.Add((name, unique, origin));
                }
            }
        }

        var indexes = new List<IndexSchema>(definitions.Count);
        foreach (var (name, unique, origin) in definitions)
        {
            var columns = new List<string>();
            await using (var command = connection.CreateCommand())
            {
                command.CommandText = "PRAGMA index_info(" + QuoteIdentifier(name) + ");";
                await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    if (!reader.IsDBNull(2))
                    {
                        columns.Add(reader.GetString(2));
                    }
                }
            }

            indexes.Add(new IndexSchema(name, columns, unique, origin.Equals("pk", StringComparison.OrdinalIgnoreCase)));
        }

        return indexes;
    }

    private async Task<IReadOnlyList<ForeignKeySchema>> ReadForeignKeysAsync(
        DbConnection connection,
        string table,
        CancellationToken cancellationToken)
    {
        var foreignKeys = new List<ForeignKeySchema>();
        await using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA foreign_key_list(" + QuoteIdentifier(table) + ");";
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            var id = reader.GetInt32(0);
            var referencedTable = reader.IsDBNull(2) ? string.Empty : reader.GetString(2);
            var column = reader.IsDBNull(3) ? string.Empty : reader.GetString(3);
            var referencedColumn = reader.IsDBNull(4) ? string.Empty : reader.GetString(4);
            var name = string.Create(CultureInfo.InvariantCulture, $"fk_{table}_{id}");
            foreignKeys.Add(new ForeignKeySchema(name, column, referencedTable, referencedColumn));
        }

        return foreignKeys;
    }
}

