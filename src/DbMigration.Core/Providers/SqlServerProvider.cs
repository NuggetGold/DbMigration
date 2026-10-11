using System.Data.Common;
using System.Globalization;
using System.Text;
using Microsoft.Data.SqlClient;

namespace DbMigration.Core.Providers;

public sealed class SqlServerProvider : DatabaseProviderBase
{
    public override DatabaseEngine Engine => DatabaseEngine.SqlServer;

    public override string DisplayName => "SQL Server";

    public override int? DefaultPort => 1433;

    protected override char IdentifierOpenQuote => '[';

    protected override char IdentifierCloseQuote => ']';

    protected override char IdentifierEscapeChar => ']';

    protected override bool SupportsIfNotExists => false;

    public override string BuildConnectionString(ConnectionProfile profile, bool revealPassword)
    {
        ArgumentNullException.ThrowIfNull(profile);

        var builder = new SqlConnectionStringBuilder
        {
            DataSource = BuildDataSource(profile),
            InitialCatalog = profile.Database ?? string.Empty,
            TrustServerCertificate = profile.TrustServerCertificate,
        };

        if (profile.IntegratedSecurity)
        {
            builder.IntegratedSecurity = true;
        }
        else
        {
            builder.UserID = profile.User ?? string.Empty;
            builder.Password = revealPassword ? profile.Password ?? string.Empty : string.Empty;
        }

        return builder.ToString();
    }

    public override DbConnection CreateConnection(ConnectionProfile profile) =>
        new SqlConnection(BuildConnectionString(profile, revealPassword: true));

    public override string MapToNativeType(CanonicalType type, int? maxLength) => type switch
    {
        CanonicalType.Boolean => "bit",
        CanonicalType.Integer => "int",
        CanonicalType.BigInt => "bigint",
        CanonicalType.Real => "float",
        CanonicalType.Decimal => "decimal(18,4)",
        CanonicalType.Text => maxLength is > 0 and <= 4000
            ? string.Create(CultureInfo.InvariantCulture, $"nvarchar({maxLength.Value})")
            : "nvarchar(max)",
        CanonicalType.Date => "date",
        CanonicalType.DateTime => "datetime2",
        CanonicalType.Time => "time",
        CanonicalType.Uuid => "uniqueidentifier",
        CanonicalType.Json => "nvarchar(max)",
        CanonicalType.Blob => "varbinary(max)",
        _ => "nvarchar(max)",
    };

    public override async Task<IReadOnlyList<TableSchema>> GetSchemaAsync(
        ConnectionProfile profile,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(profile);

        await using var connection = CreateConnection(profile);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        var columns = await ReadColumnsAsync(connection, cancellationToken).ConfigureAwait(false);
        var indexes = await ReadIndexesAsync(connection, cancellationToken).ConfigureAwait(false);
        var foreignKeys = await ReadForeignKeysAsync(connection, cancellationToken).ConfigureAwait(false);

        var result = new List<TableSchema>(columns.Count);
        foreach (var (table, tableColumns) in columns)
        {
            cancellationToken.ThrowIfCancellationRequested();
            indexes.TryGetValue(table, out var tableIndexes);
            foreignKeys.TryGetValue(table, out var tableForeignKeys);

            var rowCount = await GetRowCountAsync(
                profile,
                table.Schema + "." + table.Name,
                cancellationToken).ConfigureAwait(false);

            result.Add(new TableSchema(
                table.Name,
                table.Schema,
                tableColumns,
                tableIndexes ?? new List<IndexSchema>(),
                tableForeignKeys ?? new List<ForeignKeySchema>(),
                rowCount));
        }

        return result;
    }

    protected override string BuildColumnDefinition(ColumnSchema column, bool inlinePrimaryKey)
    {
        ArgumentNullException.ThrowIfNull(column);

        var builder = new StringBuilder();
        builder.Append(QuoteIdentifier(column.Name)).Append(' ').Append(MapToNativeType(column.Type, column.MaxLength));

        if (column.IsAutoIncrement)
        {
            builder.Append(" IDENTITY(1,1)");
        }

        if (!column.IsNullable || column.IsPrimaryKey)
        {
            builder.Append(" NOT NULL");
        }

        var defaultValue = FormatDefaultValue(column);
        if (defaultValue is not null && !column.IsAutoIncrement)
        {
            builder.Append(" DEFAULT ").Append(defaultValue);
        }

        return builder.ToString();
    }

    protected override string WrapCreateTableIfNotExists(string createSql, TableSchema table)
    {
        ArgumentNullException.ThrowIfNull(table);
        var objectName = string.IsNullOrEmpty(table.Schema)
            ? QuoteIdentifier(table.Name)
            : QuoteIdentifier(table.Schema) + "." + QuoteIdentifier(table.Name);
        var escaped = objectName.Replace("'", "''", StringComparison.Ordinal);
        return "IF OBJECT_ID(N'" + escaped + "', N'U') IS NULL\nBEGIN\n" + createSql + "\nEND;";
    }

    protected override async Task<string?> DescribeServerAsync(DbConnection connection, CancellationToken cancellationToken)
    {
        var version = await ExecuteScalarStringAsync(
            connection,
            "SELECT CAST(SERVERPROPERTY('ProductVersion') AS nvarchar(128));",
            cancellationToken).ConfigureAwait(false);
        return version is null ? "SQL Server" : "SQL Server " + version;
    }

    private static string BuildDataSource(ConnectionProfile profile)
    {
        var host = string.IsNullOrWhiteSpace(profile.Host) ? "localhost" : profile.Host;
        return profile.Port is > 0 ? host + "," + profile.Port.Value.ToString(CultureInfo.InvariantCulture) : host;
    }

    private static async Task<Dictionary<(string Schema, string Name), List<ColumnSchema>>> ReadColumnsAsync(
        DbConnection connection,
        CancellationToken cancellationToken)
    {
        var identities = await ReadIdentityColumnsAsync(connection, cancellationToken).ConfigureAwait(false);
        var map = new Dictionary<(string, string), List<ColumnSchema>>();
        await using var command = connection.CreateCommand();
        command.CommandText =
            "SELECT c.TABLE_SCHEMA, c.TABLE_NAME, c.COLUMN_NAME, c.DATA_TYPE, c.IS_NULLABLE, " +
            "c.COLUMN_DEFAULT, c.ORDINAL_POSITION, c.CHARACTER_MAXIMUM_LENGTH " +
            "FROM INFORMATION_SCHEMA.COLUMNS c " +
            "INNER JOIN INFORMATION_SCHEMA.TABLES t " +
            "ON t.TABLE_SCHEMA = c.TABLE_SCHEMA AND t.TABLE_NAME = c.TABLE_NAME " +
            "WHERE t.TABLE_TYPE = 'BASE TABLE' " +
            "ORDER BY c.TABLE_SCHEMA, c.TABLE_NAME, c.ORDINAL_POSITION;";
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            var schema = reader.GetString(0);
            var table = reader.GetString(1);
            var name = reader.GetString(2);
            var dataType = reader.GetString(3);
            var nullable = reader.GetString(4).Equals("YES", StringComparison.OrdinalIgnoreCase);
            var defaultValue = reader.IsDBNull(5) ? null : reader.GetString(5);
            var ordinal = Convert.ToInt32(reader.GetValue(6), CultureInfo.InvariantCulture);
            var maxLength = reader.IsDBNull(7) ? (int?)null : Convert.ToInt32(reader.GetValue(7), CultureInfo.InvariantCulture);
            if (maxLength == -1)
            {
                maxLength = null;
            }

            var column = new ColumnSchema(
                name,
                dataType,
                CanonicalTypeMapper.Infer(dataType),
                nullable,
                false,
                identities.Contains((schema, table, name)),
                ordinal,
                defaultValue,
                maxLength);

            var key = (schema, table);
            if (!map.TryGetValue(key, out var list))
            {
                list = new List<ColumnSchema>();
                map[key] = list;
            }

            list.Add(column);
        }

        await MarkPrimaryKeysAsync(connection, map, cancellationToken).ConfigureAwait(false);
        return map;
    }

    private static async Task<HashSet<(string Schema, string Table, string Column)>> ReadIdentityColumnsAsync(
        DbConnection connection,
        CancellationToken cancellationToken)
    {
        var set = new HashSet<(string, string, string)>();
        await using var command = connection.CreateCommand();
        command.CommandText =
            "SELECT s.name, t.name, c.name FROM sys.identity_columns c " +
            "JOIN sys.tables t ON t.object_id = c.object_id " +
            "JOIN sys.schemas s ON s.schema_id = t.schema_id;";
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            set.Add((reader.GetString(0), reader.GetString(1), reader.GetString(2)));
        }

        return set;
    }

    private static async Task MarkPrimaryKeysAsync(
        DbConnection connection,
        Dictionary<(string Schema, string Name), List<ColumnSchema>> map,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText =
            "SELECT s.name, t.name, col.name FROM sys.indexes i " +
            "JOIN sys.tables t ON t.object_id = i.object_id " +
            "JOIN sys.schemas s ON s.schema_id = t.schema_id " +
            "JOIN sys.index_columns ic ON ic.object_id = i.object_id AND ic.index_id = i.index_id " +
            "JOIN sys.columns col ON col.object_id = ic.object_id AND col.column_id = ic.column_id " +
            "WHERE i.is_primary_key = 1 AND i.type > 0;";
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            var key = (reader.GetString(0), reader.GetString(1));
            if (!map.TryGetValue(key, out var list))
            {
                continue;
            }

            var columnName = reader.GetString(2);
            for (var i = 0; i < list.Count; i++)
            {
                if (list[i].Name.Equals(columnName, StringComparison.Ordinal))
                {
                    list[i] = list[i] with { IsPrimaryKey = true, IsNullable = false };
                }
            }
        }
    }

    private static async Task<Dictionary<(string Schema, string Name), List<IndexSchema>>> ReadIndexesAsync(
        DbConnection connection,
        CancellationToken cancellationToken)
    {
        var raw = new List<(string Schema, string Table, string Name, bool Unique, bool Primary, string Column, int Ordinal)>();
        await using (var command = connection.CreateCommand())
        {
            command.CommandText =
                "SELECT s.name, t.name, i.name, i.is_unique, i.is_primary_key, col.name, ic.key_ordinal " +
                "FROM sys.indexes i " +
                "JOIN sys.tables t ON t.object_id = i.object_id " +
                "JOIN sys.schemas s ON s.schema_id = t.schema_id " +
                "JOIN sys.index_columns ic ON ic.object_id = i.object_id AND ic.index_id = i.index_id " +
                "JOIN sys.columns col ON col.object_id = ic.object_id AND col.column_id = ic.column_id " +
                "WHERE i.type > 0 AND i.name IS NOT NULL " +
                "ORDER BY s.name, t.name, i.name, ic.key_ordinal;";
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                raw.Add((
                    reader.GetString(0),
                    reader.GetString(1),
                    reader.GetString(2),
                    reader.GetBoolean(3),
                    reader.GetBoolean(4),
                    reader.GetString(5),
                    Convert.ToInt32(reader.GetValue(6), CultureInfo.InvariantCulture)));
            }
        }

        var map = new Dictionary<(string, string), List<IndexSchema>>();
        foreach (var group in raw.GroupBy(static r => (r.Schema, r.Table, r.Name)))
        {
            var columns = group.OrderBy(static r => r.Ordinal).Select(static r => r.Column).ToList();
            var index = new IndexSchema(group.Key.Name, columns, group.First().Unique, group.First().Primary);
            var key = (group.Key.Schema, group.Key.Table);
            if (!map.TryGetValue(key, out var list))
            {
                list = new List<IndexSchema>();
                map[key] = list;
            }

            list.Add(index);
        }

        return map;
    }

    private static async Task<Dictionary<(string Schema, string Name), List<ForeignKeySchema>>> ReadForeignKeysAsync(
        DbConnection connection,
        CancellationToken cancellationToken)
    {
        var map = new Dictionary<(string, string), List<ForeignKeySchema>>();
        await using var command = connection.CreateCommand();
        command.CommandText =
            "SELECT s.name, t.name, fk.name, col.name, rt.name, rcol.name " +
            "FROM sys.foreign_keys fk " +
            "JOIN sys.tables t ON t.object_id = fk.parent_object_id " +
            "JOIN sys.schemas s ON s.schema_id = t.schema_id " +
            "JOIN sys.foreign_key_columns fkc ON fkc.constraint_object_id = fk.object_id " +
            "JOIN sys.columns col ON col.object_id = fkc.parent_object_id AND col.column_id = fkc.parent_column_id " +
            "JOIN sys.tables rt ON rt.object_id = fk.referenced_object_id " +
            "JOIN sys.columns rcol ON rcol.object_id = fkc.referenced_object_id AND rcol.column_id = fkc.referenced_column_id " +
            "ORDER BY s.name, t.name, fk.name, fkc.constraint_column_id;";
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            var key = (reader.GetString(0), reader.GetString(1));
            var foreignKey = new ForeignKeySchema(
                reader.GetString(2),
                reader.GetString(3),
                reader.GetString(4),
                reader.GetString(5));

            if (!map.TryGetValue(key, out var list))
            {
                list = new List<ForeignKeySchema>();
                map[key] = list;
            }

            list.Add(foreignKey);
        }

        return map;
    }
}

