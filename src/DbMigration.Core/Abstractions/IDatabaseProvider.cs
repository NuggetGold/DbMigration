using System.Data.Common;

namespace DbMigration.Core.Abstractions;

public interface IDatabaseProvider
{
    DatabaseEngine Engine { get; }

    string DisplayName { get; }

    int? DefaultPort { get; }

    bool IsFileBased { get; }

    string BuildConnectionString(ConnectionProfile profile, bool revealPassword);

    DbConnection CreateConnection(ConnectionProfile profile);

    string QuoteIdentifier(string identifier);

    string MapToNativeType(CanonicalType type, int? maxLength);

    string BuildCreateTableSql(TableSchema table, bool ifNotExists);

    string BuildInsertSql(string table, IReadOnlyList<string> columns);

    Task<string> TestConnectionAsync(ConnectionProfile profile, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<TableSchema>> GetSchemaAsync(ConnectionProfile profile, CancellationToken cancellationToken = default);

    Task<QueryResult> ExecuteQueryAsync(ConnectionProfile profile, string sql, int maxRows, CancellationToken cancellationToken = default);

    Task<int> ExecuteNonQueryAsync(ConnectionProfile profile, string sql, CancellationToken cancellationToken = default);

    IAsyncEnumerable<object?[]> ReadTableAsync(
        ConnectionProfile profile,
        string table,
        IReadOnlyList<ColumnSchema> columns,
        int batchSize,
        CancellationToken cancellationToken = default);

    Task<long?> GetRowCountAsync(ConnectionProfile profile, string table, CancellationToken cancellationToken = default);
}

