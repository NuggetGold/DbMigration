using System.Data.Common;
using System.Diagnostics;
using System.Text;
using DbMigration.Core.Providers;
using DbMigration.Core.Sql;

namespace DbMigration.Core.Migration;

public sealed class MigrationEngine : IMigrationEngine
{
    private const int ScriptRowLimit = 100_000;

    private readonly IDatabaseProviderFactory _providers;

    public MigrationEngine(IDatabaseProviderFactory providers)
    {
        _providers = providers ?? throw new ArgumentNullException(nameof(providers));
    }

    public MigrationResult? LastResult { get; private set; }

    public async Task<MigrationResult> MigrateAsync(
        MigrationRequest request,
        IProgress<MigrationProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        LastResult = null;
        var stopwatch = Stopwatch.StartNew();
        var sourceProvider = _providers.Get(request.Source.Engine);
        var targetProvider = _providers.Get(request.Target.Engine);

        var sourceSchema = await sourceProvider.GetSchemaAsync(request.Source, cancellationToken).ConfigureAwait(false);
        var selected = SelectTables(sourceSchema, request.Tables);

        var targetSchema = await targetProvider.GetSchemaAsync(request.Target, cancellationToken).ConfigureAwait(false);
        var targetTables = new HashSet<string>(targetSchema.Select(static t => t.Name), StringComparer.OrdinalIgnoreCase);

        var results = new List<TableMigrationResult>(selected.Count);

        try
        {
            for (var index = 0; index < selected.Count; index++)
            {
                var table = selected[index];
                cancellationToken.ThrowIfCancellationRequested();

                var tableNumber = index + 1;
                progress?.Report(new MigrationProgress(
                    table.Name,
                    tableNumber,
                    selected.Count,
                    0,
                    table.RowCount,
                    $"Миграция {table.Name}..."));

                var tableStopwatch = Stopwatch.StartNew();
                try
                {
                    var copied = await MigrateTableAsync(
                        sourceProvider,
                        targetProvider,
                        request,
                        table,
                        targetTables,
                        progress,
                        tableNumber,
                        selected.Count,
                        cancellationToken).ConfigureAwait(false);

                    tableStopwatch.Stop();
                    results.Add(new TableMigrationResult(table.Name, true, copied, tableStopwatch.Elapsed));
                    progress?.Report(new MigrationProgress(
                        table.Name,
                        tableNumber,
                        selected.Count,
                        copied,
                        table.RowCount,
                        $"Таблица {table.Name} перенесена ({copied} строк)."));
                }
                catch (Exception ex) when (ex is DbException or NotSupportedException or InvalidOperationException or ArgumentException)
                {
                    tableStopwatch.Stop();
                    var failure = new TableMigrationResult(table.Name, false, 0, tableStopwatch.Elapsed, ex.Message);
                    results.Add(failure);
                    progress?.Report(new MigrationProgress(
                        table.Name,
                        tableNumber,
                        selected.Count,
                        0,
                        table.RowCount,
                        $"Ошибка в {table.Name}: {ex.Message}"));

                    if (request.StopOnFirstError)
                    {
                        stopwatch.Stop();
                        return new MigrationResult(false, results, stopwatch.Elapsed, ex.Message);
                    }
                }
            }
        }
        catch (OperationCanceledException)
        {
            LastResult = new MigrationResult(false, results, stopwatch.Elapsed, "Миграция отменена.");
            throw;
        }

        stopwatch.Stop();
        var success = results.All(static r => r.Success);
        return new MigrationResult(success, results, stopwatch.Elapsed);
    }

    public async Task<string> GenerateSqlScriptAsync(MigrationRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var sourceProvider = _providers.Get(request.Source.Engine);
        var targetProvider = _providers.Get(request.Target.Engine);

        var sourceSchema = await sourceProvider.GetSchemaAsync(request.Source, cancellationToken).ConfigureAwait(false);
        var selected = SelectTables(sourceSchema, request.Tables);

        var schemaScope = request.Scope.HasFlag(MigrationScope.Schema);
        var dataScope = request.Scope.HasFlag(MigrationScope.Data);

        var builder = new StringBuilder();
        builder.AppendLine("-- DbMigration generated script");
        builder.AppendLine($"-- Source : {sourceProvider.DisplayName} ({request.Source.DisplayTarget})");
        builder.AppendLine($"-- Target : {targetProvider.DisplayName} ({request.Target.DisplayTarget})");
        builder.AppendLine($"-- Generated: {DateTimeOffset.UtcNow:O}");
        builder.AppendLine($"-- Tables : {selected.Count}");
        builder.AppendLine();

        foreach (var table in selected)
        {
            cancellationToken.ThrowIfCancellationRequested();

            builder.AppendLine($"-- Table: {table.QualifiedName}");

            if (schemaScope)
            {
                builder.AppendLine(targetProvider.BuildCreateTableSql(table, ifNotExists: true));
                builder.AppendLine();
            }

            if (!dataScope)
            {
                continue;
            }

            var columns = table.Columns.OrderBy(static c => c.Ordinal).ToList();
            if (columns.Count == 0)
            {
                continue;
            }

            var header = "INSERT INTO " + targetProvider.QuoteQualifiedName(table.QualifiedName)
                + " (" + string.Join(", ", columns.Select(c => targetProvider.QuoteIdentifier(c.Name))) + ") VALUES (";

            long count = 0;
            await foreach (var row in sourceProvider
                .ReadTableAsync(request.Source, table.QualifiedName, columns, 500, cancellationToken)
                .WithCancellation(cancellationToken)
                .ConfigureAwait(false))
            {
                if (count >= ScriptRowLimit)
                {
                    builder.AppendLine($"-- Truncated: only the first {ScriptRowLimit} rows of {table.QualifiedName} were written.");
                    break;
                }

                var literals = new string[row.Length];
                for (var i = 0; i < row.Length; i++)
                {
                    literals[i] = SqlLiteralFormatter.Format(row[i], request.Target.Engine);
                }

                builder.Append(header).Append(string.Join(", ", literals)).AppendLine(");");
                count++;
            }

            builder.AppendLine($"-- Rows: {count}");
            builder.AppendLine();
        }

        return builder.ToString();
    }

    public async Task<IReadOnlyList<string>> GetTableNamesAsync(ConnectionProfile profile, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(profile);

        var provider = _providers.Get(profile.Engine);
        var schema = await provider.GetSchemaAsync(profile, cancellationToken).ConfigureAwait(false);
        return schema.Select(static t => t.QualifiedName).ToList();
    }

    private static IReadOnlyList<TableSchema> SelectTables(IReadOnlyList<TableSchema> schema, IReadOnlyList<string> requested)
    {
        if (requested.Count == 0)
        {
            return schema;
        }

        var lookup = new Dictionary<string, TableSchema>(StringComparer.OrdinalIgnoreCase);
        foreach (var table in schema)
        {
            lookup[table.Name] = table;
            lookup[table.QualifiedName] = table;
        }

        var selected = new List<TableSchema>(requested.Count);
        foreach (var name in requested)
        {
            if (lookup.TryGetValue(name, out var table))
            {
                selected.Add(table);
            }
        }

        return selected;
    }

    private static async Task<long> MigrateTableAsync(
        IDatabaseProvider sourceProvider,
        IDatabaseProvider targetProvider,
        MigrationRequest request,
        TableSchema table,
        HashSet<string> targetTables,
        IProgress<MigrationProgress>? progress,
        int tableIndex,
        int tableCount,
        CancellationToken cancellationToken)
    {
        if (request.Scope == MigrationScope.None)
        {
            return 0;
        }

        var schemaScope = request.Scope.HasFlag(MigrationScope.Schema);
        var dataScope = request.Scope.HasFlag(MigrationScope.Data);
        var quotedName = targetProvider.QuoteQualifiedName(table.QualifiedName);
        var exists = targetTables.Contains(table.Name);

        if (exists)
        {
            switch (request.ExistingTables)
            {
                case ExistingTableBehavior.Fail:
                    throw new InvalidOperationException($"Целевая таблица '{table.QualifiedName}' уже существует.");
                case ExistingTableBehavior.Skip:
                    return 0;
                case ExistingTableBehavior.DropAndRecreate:
                    await targetProvider
                        .ExecuteNonQueryAsync(request.Target, "DROP TABLE " + quotedName + ";", cancellationToken)
                        .ConfigureAwait(false);
                    targetTables.Remove(table.Name);
                    exists = false;
                    break;
                case ExistingTableBehavior.TruncateAndAppend:
                    await targetProvider
                        .ExecuteNonQueryAsync(request.Target, "DELETE FROM " + quotedName + ";", cancellationToken)
                        .ConfigureAwait(false);
                    break;
                default:
                    throw new NotSupportedException($"Неподдерживаемое поведение для существующих таблиц: '{request.ExistingTables}'.");
            }
        }

        if (!exists && (schemaScope || dataScope))
        {
            var createSql = targetProvider.BuildCreateTableSql(table, ifNotExists: true);
            await targetProvider.ExecuteNonQueryAsync(request.Target, createSql, cancellationToken).ConfigureAwait(false);
            targetTables.Add(table.Name);
        }

        if (!dataScope)
        {
            return 0;
        }

        var columns = table.Columns.OrderBy(static c => c.Ordinal).ToList();
        if (columns.Count == 0)
        {
            return 0;
        }

        return await CopyDataAsync(
            sourceProvider,
            targetProvider,
            request,
            table,
            columns,
            progress,
            tableIndex,
            tableCount,
            cancellationToken).ConfigureAwait(false);
    }

    private static async Task<long> CopyDataAsync(
        IDatabaseProvider sourceProvider,
        IDatabaseProvider targetProvider,
        MigrationRequest request,
        TableSchema table,
        IReadOnlyList<ColumnSchema> columns,
        IProgress<MigrationProgress>? progress,
        int tableIndex,
        int tableCount,
        CancellationToken cancellationToken)
    {
        var batchSize = request.BatchSize > 0 ? request.BatchSize : 500;
        var insertSql = targetProvider.BuildInsertSql(table.QualifiedName, columns.Select(static c => c.Name).ToList());
        var useIdentityInsert = targetProvider.Engine == DatabaseEngine.SqlServer
            && columns.Any(static c => c.IsAutoIncrement);

        long copied = 0;
        var batch = new List<object?[]>(batchSize);

        await using var connection = targetProvider.CreateConnection(request.Target);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = insertSql;
        for (var i = 0; i < columns.Count; i++)
        {
            var parameter = command.CreateParameter();
            parameter.ParameterName = "@p" + i;
            command.Parameters.Add(parameter);
        }

        if (useIdentityInsert)
        {
            await ExecuteAsync(connection, null, "SET IDENTITY_INSERT " + targetProvider.QuoteQualifiedName(table.QualifiedName) + " ON;", cancellationToken).ConfigureAwait(false);
        }

        try
        {
            await foreach (var row in sourceProvider
                .ReadTableAsync(request.Source, table.QualifiedName, columns, batchSize, cancellationToken)
                .WithCancellation(cancellationToken)
                .ConfigureAwait(false))
            {
                batch.Add(row);
                if (batch.Count < batchSize)
                {
                    continue;
                }

                copied += await FlushBatchAsync(connection, command, batch, cancellationToken).ConfigureAwait(false);
                batch.Clear();
                progress?.Report(new MigrationProgress(
                    table.Name,
                    tableIndex,
                    tableCount,
                    copied,
                    table.RowCount,
                    $"Скопировано {copied} строк в {table.Name}."));
            }

            if (batch.Count > 0)
            {
                copied += await FlushBatchAsync(connection, command, batch, cancellationToken).ConfigureAwait(false);
                batch.Clear();
            }
        }
        finally
        {
            if (useIdentityInsert)
            {
                await ExecuteAsync(connection, null, "SET IDENTITY_INSERT " + targetProvider.QuoteQualifiedName(table.QualifiedName) + " OFF;", CancellationToken.None).ConfigureAwait(false);
            }
        }

        return copied;
    }

    private static async Task<long> FlushBatchAsync(
        DbConnection connection,
        DbCommand command,
        List<object?[]> batch,
        CancellationToken cancellationToken)
    {
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        command.Transaction = transaction;

        long inserted = 0;
        foreach (var row in batch)
        {
            cancellationToken.ThrowIfCancellationRequested();
            for (var i = 0; i < command.Parameters.Count; i++)
            {
                command.Parameters[i].Value = i < row.Length ? row[i] ?? DBNull.Value : DBNull.Value;
            }

            await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            inserted++;
        }

        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return inserted;
    }

    private static async Task ExecuteAsync(
        DbConnection connection,
        DbTransaction? transaction,
        string sql,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.Transaction = transaction;
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }
}

