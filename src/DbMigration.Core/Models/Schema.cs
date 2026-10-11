namespace DbMigration.Core.Models;

public sealed record ColumnSchema(
    string Name,
    string NativeType,
    CanonicalType Type,
    bool IsNullable,
    bool IsPrimaryKey,
    bool IsAutoIncrement,
    int Ordinal,
    string? DefaultValue = null,
    int? MaxLength = null)
{
    public bool IsRequired => !IsNullable;
}

public sealed record IndexSchema(
    string Name,
    IReadOnlyList<string> Columns,
    bool IsUnique,
    bool IsPrimary);

public sealed record ForeignKeySchema(
    string Name,
    string Column,
    string ReferencedTable,
    string ReferencedColumn);

public sealed record TableSchema(
    string Name,
    string? Schema,
    IReadOnlyList<ColumnSchema> Columns,
    IReadOnlyList<IndexSchema> Indexes,
    IReadOnlyList<ForeignKeySchema> ForeignKeys,
    long? RowCount = null)
{
    public string QualifiedName => string.IsNullOrEmpty(Schema) ? Name : $"{Schema}.{Name}";

    public IReadOnlyList<ColumnSchema> PrimaryKey => Columns.Where(c => c.IsPrimaryKey).OrderBy(c => c.Ordinal).ToList();
}

