namespace DbMigration.Core.Models;

public sealed class QueryResult
{
    public IReadOnlyList<string> Columns { get; init; } = Array.Empty<string>();

    public IReadOnlyList<object?[]> Rows { get; init; } = Array.Empty<object?[]>();

    public int? RecordsAffected { get; init; }

    public bool HasRows { get; init; }

    public bool Truncated { get; init; }

    public TimeSpan Elapsed { get; init; }

    public static QueryResult Empty(TimeSpan elapsed) => new() { Elapsed = elapsed };

    public static QueryResult NonQuery(int affected, TimeSpan elapsed) =>
        new() { RecordsAffected = affected, Elapsed = elapsed };
}

