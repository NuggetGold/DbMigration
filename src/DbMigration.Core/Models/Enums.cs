namespace DbMigration.Core.Models;

public enum DatabaseEngine
{
    Sqlite,
    MySql,
    PostgreSql,
    SqlServer,
}

public enum CanonicalType
{
    Unknown,
    Boolean,
    Integer,
    BigInt,
    Real,
    Decimal,
    Text,
    Date,
    DateTime,
    Time,
    Uuid,
    Json,
    Blob,
}

[Flags]
public enum MigrationScope
{
    None = 0,
    Schema = 1,
    Data = 2,
    SchemaAndData = Schema | Data,
}

public enum ExistingTableBehavior
{
    Fail,
    Skip,
    DropAndRecreate,
    TruncateAndAppend,
}

public enum LogLevel
{
    Trace,
    Info,
    Success,
    Warning,
    Error,
}

public enum ExportFormat
{
    SqlScript,
    Csv,
    Json,
}

