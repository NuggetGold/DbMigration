namespace DbMigration.Core.Models;

public sealed record MigrationRequest
{
    public required ConnectionProfile Source { get; init; }

    public required ConnectionProfile Target { get; init; }

    public IReadOnlyList<string> Tables { get; init; } = Array.Empty<string>();

    public MigrationScope Scope { get; init; } = MigrationScope.SchemaAndData;

    public ExistingTableBehavior ExistingTables { get; init; } = ExistingTableBehavior.Skip;

    public int BatchSize { get; init; } = 500;

    public bool UnorderedBulkCopy { get; init; } = true;

    public bool StopOnFirstError { get; init; }
}

public sealed record MigrationProgress(
    string Table,
    int TableIndex,
    int TableCount,
    long RowsCopied,
    long? RowsTotal,
    string Message)
{
    public double Fraction => RowsTotal is > 0
        ? Math.Clamp((double)RowsCopied / RowsTotal.Value, 0, 1)
        : 0;
}

public sealed record TableMigrationResult(
    string Table,
    bool Success,
    long RowsCopied,
    TimeSpan Elapsed,
    string? Error = null);

public sealed record MigrationResult(
    bool Success,
    IReadOnlyList<TableMigrationResult> Tables,
    TimeSpan Elapsed,
    string? FatalError = null)
{
    public long TotalRowsCopied => Tables.Sum(t => t.RowsCopied);

    public int SucceededTables => Tables.Count(t => t.Success);
}

