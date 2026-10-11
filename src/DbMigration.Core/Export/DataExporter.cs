using System.Globalization;
using System.Text;
using System.Text.Json;
using DbMigration.Core.Providers;
using DbMigration.Core.Sql;

namespace DbMigration.Core.Export;

public sealed class DataExporter : IDataExporter
{
    private const int ProgressInterval = 500;
    private static readonly char[] CsvSpecialChars = { ',', '"', '\n', '\r' };

    private readonly IDatabaseProviderFactory _providers;

    public DataExporter(IDatabaseProviderFactory providers)
    {
        _providers = providers ?? throw new ArgumentNullException(nameof(providers));
    }

    public string GetExtension(ExportFormat format) => format switch
    {
        ExportFormat.SqlScript => "sql",
        ExportFormat.Csv => "csv",
        ExportFormat.Json => "json",
        _ => "txt",
    };

    public string GetDisplayName(ExportFormat format) => format switch
    {
        ExportFormat.SqlScript => "SQL-скрипт",
        ExportFormat.Csv => "CSV",
        ExportFormat.Json => "JSON",
        _ => format.ToString(),
    };

    public Task ExportTableAsync(
        ConnectionProfile source,
        TableSchema table,
        ExportFormat format,
        Stream output,
        bool includeData,
        int maxRows,
        IProgress<long>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(table);
        ArgumentNullException.ThrowIfNull(output);

        var provider = _providers.Get(source.Engine);

        return format switch
        {
            ExportFormat.SqlScript => ExportSqlAsync(provider, source, table, output, includeData, maxRows, progress, cancellationToken),
            ExportFormat.Csv => ExportCsvAsync(provider, source, table, output, includeData, maxRows, progress, cancellationToken),
            ExportFormat.Json => ExportJsonAsync(provider, source, table, output, includeData, maxRows, progress, cancellationToken),
            _ => throw new NotSupportedException($"Unsupported export format '{format}'."),
        };
    }

    private static async Task ExportSqlAsync(
        IDatabaseProvider provider,
        ConnectionProfile source,
        TableSchema table,
        Stream output,
        bool includeData,
        int maxRows,
        IProgress<long>? progress,
        CancellationToken cancellationToken)
    {
        var columns = table.Columns.OrderBy(static c => c.Ordinal).ToList();

        await using var writer = new StreamWriter(output, new UTF8Encoding(false), leaveOpen: true);
        await writer.WriteLineAsync("-- DbMigration export").ConfigureAwait(false);
        await writer.WriteLineAsync($"-- Table: {table.QualifiedName}").ConfigureAwait(false);
        await writer.WriteLineAsync($"-- Engine: {provider.DisplayName}").ConfigureAwait(false);
        await writer.WriteLineAsync($"-- Generated: {DateTimeOffset.UtcNow:O}").ConfigureAwait(false);
        await writer.WriteLineAsync().ConfigureAwait(false);
        await writer.WriteLineAsync(provider.BuildCreateTableSql(table, ifNotExists: true)).ConfigureAwait(false);
        await writer.WriteLineAsync().ConfigureAwait(false);

        if (!includeData || columns.Count == 0)
        {
            await writer.FlushAsync(cancellationToken).ConfigureAwait(false);
            return;
        }

        var header = "INSERT INTO " + provider.QuoteQualifiedName(table.QualifiedName)
            + " (" + string.Join(", ", columns.Select(c => provider.QuoteIdentifier(c.Name))) + ") VALUES (";

        var count = await WriteRowsAsync(
            provider,
            source,
            table,
            columns,
            maxRows,
            progress,
            cancellationToken,
            async (row, _) =>
            {
                var literals = new string[row.Length];
                for (var i = 0; i < row.Length; i++)
                {
                    literals[i] = SqlLiteralFormatter.Format(row[i], source.Engine);
                }

                await writer.WriteLineAsync(header + string.Join(", ", literals) + ");").ConfigureAwait(false);
            }).ConfigureAwait(false);

        await writer.WriteLineAsync($"-- Rows: {count}").ConfigureAwait(false);
        await writer.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    private static async Task ExportCsvAsync(
        IDatabaseProvider provider,
        ConnectionProfile source,
        TableSchema table,
        Stream output,
        bool includeData,
        int maxRows,
        IProgress<long>? progress,
        CancellationToken cancellationToken)
    {
        var columns = table.Columns.OrderBy(static c => c.Ordinal).ToList();

        await using var writer = new StreamWriter(output, new UTF8Encoding(false), leaveOpen: true);
        await writer.WriteLineAsync(FormatCsvRow(columns.Select(static c => (object?)c.Name))).ConfigureAwait(false);

        if (!includeData || columns.Count == 0)
        {
            await writer.FlushAsync(cancellationToken).ConfigureAwait(false);
            return;
        }

        await WriteRowsAsync(
            provider,
            source,
            table,
            columns,
            maxRows,
            progress,
            cancellationToken,
            async (row, _) =>
            {
                await writer.WriteLineAsync(FormatCsvRow(row)).ConfigureAwait(false);
            }).ConfigureAwait(false);

        await writer.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    private static async Task ExportJsonAsync(
        IDatabaseProvider provider,
        ConnectionProfile source,
        TableSchema table,
        Stream output,
        bool includeData,
        int maxRows,
        IProgress<long>? progress,
        CancellationToken cancellationToken)
    {
        var columns = table.Columns.OrderBy(static c => c.Ordinal).ToList();

        using var json = new Utf8JsonWriter(output, new JsonWriterOptions { Indented = true });
        json.WriteStartArray();

        if (includeData && columns.Count > 0)
        {
            await WriteRowsAsync(
                provider,
                source,
                table,
                columns,
                maxRows,
                progress,
                cancellationToken,
                (row, _) =>
                {
                    json.WriteStartObject();
                    for (var i = 0; i < row.Length; i++)
                    {
                        WriteJsonValue(json, columns[i].Name, row[i]);
                    }

                    json.WriteEndObject();
                    return Task.CompletedTask;
                }).ConfigureAwait(false);
        }

        json.WriteEndArray();
        await json.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    private static async Task<long> WriteRowsAsync(
        IDatabaseProvider provider,
        ConnectionProfile source,
        TableSchema table,
        IReadOnlyList<ColumnSchema> columns,
        int maxRows,
        IProgress<long>? progress,
        CancellationToken cancellationToken,
        Func<object?[], CancellationToken, Task> write)
    {
        long count = 0;
        await foreach (var row in provider
            .ReadTableAsync(source, table.QualifiedName, columns, ProgressInterval, cancellationToken)
            .WithCancellation(cancellationToken)
            .ConfigureAwait(false))
        {
            if (maxRows > 0 && count >= maxRows)
            {
                break;
            }

            await write(row, cancellationToken).ConfigureAwait(false);
            count++;

            if (progress is not null && count % ProgressInterval == 0)
            {
                progress.Report(count);
            }
        }

        progress?.Report(count);
        return count;
    }

    private static string FormatCsvRow(IEnumerable<object?> values)
    {
        var builder = new StringBuilder();
        var first = true;
        foreach (var value in values)
        {
            if (!first)
            {
                builder.Append(',');
            }

            first = false;
            builder.Append(EscapeCsv(FormatCsvValue(value)));
        }

        return builder.ToString();
    }

    private static string FormatCsvValue(object? value) => value switch
    {
        null or DBNull => string.Empty,
        bool boolean => boolean ? "true" : "false",
        byte[] bytes => Convert.ToBase64String(bytes),
        DateTime dateTime => dateTime.ToString("o", CultureInfo.InvariantCulture),
        DateTimeOffset dateTimeOffset => dateTimeOffset.ToString("o", CultureInfo.InvariantCulture),
        TimeSpan timeSpan => timeSpan.ToString("c", CultureInfo.InvariantCulture),
        IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture) ?? string.Empty,
        _ => value.ToString() ?? string.Empty,
    };

    private static string EscapeCsv(string field)
    {
        if (field.IndexOfAny(CsvSpecialChars) < 0)
        {
            return field;
        }

        return string.Concat("\"", field.Replace("\"", "\"\"", StringComparison.Ordinal), "\"");
    }

    private static void WriteJsonValue(Utf8JsonWriter writer, string name, object? value)
    {
        switch (value)
        {
            case null or DBNull:
                writer.WriteNull(name);
                break;
            case bool boolean:
                writer.WriteBoolean(name, boolean);
                break;
            case byte[] bytes:
                writer.WriteString(name, Convert.ToBase64String(bytes));
                break;
            case DateTime dateTime:
                writer.WriteString(name, dateTime);
                break;
            case DateTimeOffset dateTimeOffset:
                writer.WriteString(name, dateTimeOffset);
                break;
            case Guid guid:
                writer.WriteString(name, guid);
                break;
            case string text:
                writer.WriteString(name, text);
                break;
            case char character:
                writer.WriteString(name, character.ToString());
                break;
            case decimal decimalValue:
                writer.WriteNumber(name, decimalValue);
                break;
            case double doubleValue:
                writer.WriteNumber(name, doubleValue);
                break;
            case float floatValue:
                writer.WriteNumber(name, floatValue);
                break;
            case long longValue:
                writer.WriteNumber(name, longValue);
                break;
            case ulong ulongValue:
                writer.WriteNumber(name, ulongValue);
                break;
            case int intValue:
                writer.WriteNumber(name, intValue);
                break;
            case uint uintValue:
                writer.WriteNumber(name, uintValue);
                break;
            case short shortValue:
                writer.WriteNumber(name, shortValue);
                break;
            case byte byteValue:
                writer.WriteNumber(name, byteValue);
                break;
            case sbyte sbyteValue:
                writer.WriteNumber(name, sbyteValue);
                break;
            default:
                writer.WriteString(name, Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty);
                break;
        }
    }
}

