using System.Data.Common;
using System.Globalization;
using System.Text;
using MySqlConnector;

namespace DbMigration.Core.Providers;

public sealed class MySqlProvider : DatabaseProviderBase
{
    public override DatabaseEngine Engine => DatabaseEngine.MySql;

    public override string DisplayName => "MySQL";

    public override int? DefaultPort => 3306;

    protected override char IdentifierOpenQuote => '`';

    public override string BuildConnectionString(ConnectionProfile profile, bool revealPassword)
    {
        ArgumentNullException.ThrowIfNull(profile);

        var builder = new MySqlConnectionStringBuilder
        {
            Server = string.IsNullOrWhiteSpace(profile.Host) ? "localhost" : profile.Host,
            Port = (uint)(profile.Port ?? DefaultPort ?? 3306),
            Database = profile.Database ?? string.Empty,
            UserID = profile.User ?? string.Empty,
            Password = revealPassword ? profile.Password ?? string.Empty : string.Empty,
            SslMode = MySqlSslMode.Preferred,
            AllowUserVariables = true,
        };

        return builder.ToString();
    }

    public override DbConnection CreateConnection(ConnectionProfile profile) =>
        new MySqlConnection(BuildConnectionString(profile, revealPassword: true));

    public override string MapToNativeType(CanonicalType type, int? maxLength) => type switch
    {
        CanonicalType.Boolean => "TINYINT(1)",
        CanonicalType.Integer => "INT",
        CanonicalType.BigInt => "BIGINT",
        CanonicalType.Real => "DOUBLE",
        CanonicalType.Decimal => "DECIMAL(18,4)",
        CanonicalType.Text => maxLength is > 0 and <= 65535
            ? string.Create(CultureInfo.InvariantCulture, $"VARCHAR({maxLength.Value})")
            : "TEXT",
        CanonicalType.Date => "DATE",
        CanonicalType.DateTime => "DATETIME",
        CanonicalType.Time => "TIME",
        CanonicalType.Uuid => "CHAR(36)",
        CanonicalType.Json => "JSON",
        CanonicalType.Blob => "LONGBLOB",
        _ => "TEXT",
    };

    public override async Task<IReadOnlyList<TableSchema>> GetSchemaAsync(
        ConnectionProfile profile,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(profile);

        await using var connection = CreateConnection(profile);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        var schema = profile.Database;
        if (string.IsNullOrWhiteSpace(schema))
        {
            schema = await ExecuteScalarStringAsync(connection, "SELECT DATABASE();", cancellationToken)
                .ConfigureAwait(false)
                ?? string.Empty;
        }

        var tables = await ReadTableNamesAsync(connection, schema, cancellationToken).ConfigureAwait(false);
        var columns = await ReadColumnsAsync(connection, schema, cancellationToken).ConfigureAwait(false);
        var indexes = await ReadIndexesAsync(connection, schema, cancellationToken).ConfigureAwait(false);
        var foreignKeys = await ReadForeignKeysAsync(connection, schema, cancellationToken).ConfigureAwait(false);

        var result = new List<TableSchema>(tables.Count);
        foreach (var table in tables)
        {
            cancellationToken.ThrowIfCancellationRequested();
            columns.TryGetValue(table, out var tableColumns);
            indexes.TryGetValue(table, out var tableIndexes);
            foreignKeys.TryGetValue(table, out var tableForeignKeys);

            var rowCount = await GetRowCountAsync(
                profile,
                table,
                cancellationToken).ConfigureAwait(false);

            result.Add(new TableSchema(
                table,
                null,
                tableColumns ?? new List<ColumnSchema>(),
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

        if (!column.IsNullable || column.IsPrimaryKey)
        {
            builder.Append(" NOT NULL");
        }

        var defaultValue = FormatDefaultValue(column);
        if (defaultValue is not null)
        {
            builder.Append(" DEFAULT ").Append(defaultValue);
        }

        if (column.IsAutoIncrement)
        {
            builder.Append(" AUTO_INCREMENT");
        }

        return builder.ToString();
    }

    protected override async Task<string?> DescribeServerAsync(DbConnection connection, CancellationToken cancellationToken)
    {
        var version = await ExecuteScalarStringAsync(connection, "SELECT VERSION();", cancellationToken)
            .ConfigureAwait(false);
        return version is null ? "MySQL" : "MySQL " + version;
    }

    private static async Task<List<string>> ReadTableNamesAsync(
        DbConnection connection,
        string schema,
        CancellationToken cancellationToken)
    {
        var tables = new List<string>();
        await using var command = connection.CreateCommand();
        command.CommandText =
            "SELECT TABLE_NAME FROM information_schema.TABLES " +
            "WHERE TABLE_SCHEMA = @schema AND TABLE_TYPE = 'BASE TABLE' ORDER BY TABLE_NAME;";
        AddParameter(command, "@schema", schema);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            tables.Add(reader.GetString(0));
        }

        return tables;
    }

    private static async Task<Dictionary<string, List<ColumnSchema>>> ReadColumnsAsync(
        DbConnection connection,
        string schema,
        CancellationToken cancellationToken)
    {
        var map = new Dictionary<string, List<ColumnSchema>>(StringComparer.OrdinalIgnoreCase);
        await using var command = connection.CreateCommand();
        command.CommandText =
            "SELECT TABLE_NAME, COLUMN_NAME, DATA_TYPE, COLUMN_TYPE, IS_NULLABLE, COLUMN_DEFAULT, " +
            "COLUMN_KEY, EXTRA, ORDINAL_POSITION, CHARACTER_MAXIMUM_LENGTH " +
            "FROM information_schema.COLUMNS WHERE TABLE_SCHEMA = @schema " +
            "ORDER BY TABLE_NAME, ORDINAL_POSITION;";
        AddParameter(command, "@schema", schema);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            var table = reader.GetString(0);
            var name = reader.GetString(1);
            var columnType = reader.IsDBNull(3) ? reader.GetString(2) : reader.GetString(3);
            var nullable = reader.GetString(4).Equals("YES", StringComparison.OrdinalIgnoreCase);
            var defaultValue = reader.IsDBNull(5) ? null : reader.GetString(5);
            var key = reader.IsDBNull(6) ? string.Empty : reader.GetString(6);
            var extra = reader.IsDBNull(7) ? string.Empty : reader.GetString(7);
            var ordinal = Convert.ToInt32(reader.GetValue(8), CultureInfo.InvariantCulture);
            var maxLength = reader.IsDBNull(9) ? (int?)null : Convert.ToInt32(reader.GetValue(9), CultureInfo.InvariantCulture);

            var isPrimaryKey = key.Equals("PRI", StringComparison.OrdinalIgnoreCase);
            var column = new ColumnSchema(
                name,
                columnType,
                CanonicalTypeMapper.Infer(columnType),
                nullable && !isPrimaryKey,
                isPrimaryKey,
                extra.Contains("auto_increment", StringComparison.OrdinalIgnoreCase),
                ordinal,
                defaultValue,
                maxLength);

            if (!map.TryGetValue(table, out var list))
            {
                list = new List<ColumnSchema>();
                map[table] = list;
            }

            list.Add(column);
        }

        return map;
    }

    private static async Task<Dictionary<string, List<IndexSchema>>> ReadIndexesAsync(
        DbConnection connection,
        string schema,
        CancellationToken cancellationToken)
    {
        var raw = new List<(string Table, string Name, bool Unique, int Sequence, string Column)>();
        await using (var command = connection.CreateCommand())
        {
            command.CommandText =
                "SELECT TABLE_NAME, INDEX_NAME, NON_UNIQUE, SEQ_IN_INDEX, COLUMN_NAME " +
                "FROM information_schema.STATISTICS WHERE TABLE_SCHEMA = @schema " +
                "ORDER BY TABLE_NAME, INDEX_NAME, SEQ_IN_INDEX;";
            AddParameter(command, "@schema", schema);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                raw.Add((
                    reader.GetString(0),
                    reader.GetString(1),
                    Convert.ToInt32(reader.GetValue(2), CultureInfo.InvariantCulture) == 0,
                    Convert.ToInt32(reader.GetValue(3), CultureInfo.InvariantCulture),
                    reader.IsDBNull(4) ? string.Empty : reader.GetString(4)));
            }
        }

        var map = new Dictionary<string, List<IndexSchema>>(StringComparer.OrdinalIgnoreCase);
        foreach (var group in raw.GroupBy(static r => (r.Table, r.Name)))
        {
            var columns = group.OrderBy(static r => r.Sequence).Select(static r => r.Column).ToList();
            var index = new IndexSchema(
                group.Key.Name,
                columns,
                group.First().Unique,
                group.Key.Name.Equals("PRIMARY", StringComparison.OrdinalIgnoreCase));

            if (!map.TryGetValue(group.Key.Table, out var list))
            {
                list = new List<IndexSchema>();
                map[group.Key.Table] = list;
            }

            list.Add(index);
        }

        return map;
    }

    private static async Task<Dictionary<string, List<ForeignKeySchema>>> ReadForeignKeysAsync(
        DbConnection connection,
        string schema,
        CancellationToken cancellationToken)
    {
        var map = new Dictionary<string, List<ForeignKeySchema>>(StringComparer.OrdinalIgnoreCase);
        await using var command = connection.CreateCommand();
        command.CommandText =
            "SELECT TABLE_NAME, CONSTRAINT_NAME, COLUMN_NAME, REFERENCED_TABLE_NAME, REFERENCED_COLUMN_NAME " +
            "FROM information_schema.KEY_COLUMN_USAGE " +
            "WHERE TABLE_SCHEMA = @schema AND REFERENCED_TABLE_NAME IS NOT NULL " +
            "ORDER BY TABLE_NAME, CONSTRAINT_NAME, ORDINAL_POSITION;";
        AddParameter(command, "@schema", schema);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            var table = reader.GetString(0);
            var foreignKey = new ForeignKeySchema(
                reader.GetString(1),
                reader.GetString(2),
                reader.GetString(3),
                reader.IsDBNull(4) ? string.Empty : reader.GetString(4));

            if (!map.TryGetValue(table, out var list))
            {
                list = new List<ForeignKeySchema>();
                map[table] = list;
            }

            list.Add(foreignKey);
        }

        return map;
    }

    private static void AddParameter(DbCommand command, string name, object value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value;
        command.Parameters.Add(parameter);
    }
}

