namespace DbMigration.App;

public sealed record LogEntry(DateTimeOffset Timestamp, LogLevel Level, string Message);

