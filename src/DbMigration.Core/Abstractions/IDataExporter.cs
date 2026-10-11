namespace DbMigration.Core.Abstractions;

public interface IDataExporter
{
    string GetExtension(ExportFormat format);

    string GetDisplayName(ExportFormat format);

    Task ExportTableAsync(
        ConnectionProfile source,
        TableSchema table,
        ExportFormat format,
        Stream output,
        bool includeData,
        int maxRows,
        IProgress<long>? progress = null,
        CancellationToken cancellationToken = default);
}

