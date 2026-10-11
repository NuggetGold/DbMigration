using System.Globalization;

namespace DbMigration.Core.Sql;

internal static class SqlLiteralFormatter
{
    public static string Format(object? value, DatabaseEngine engine) => value switch
    {
        null or DBNull => "NULL",
        bool boolean => FormatBoolean(boolean, engine),
        byte[] bytes => FormatBytes(bytes, engine),
        DateTime dateTime => Quote(dateTime.ToString("yyyy-MM-dd HH:mm:ss.fffffff", CultureInfo.InvariantCulture)),
        DateTimeOffset dateTimeOffset => Quote(dateTimeOffset.ToString("yyyy-MM-dd HH:mm:ss.fffffffzzz", CultureInfo.InvariantCulture)),
        TimeSpan timeSpan => Quote(timeSpan.ToString(@"hh\:mm\:ss\.fffffff", CultureInfo.InvariantCulture)),
        Guid guid => Quote(guid.ToString("D")),
        string text => Quote(text),
        char character => Quote(character.ToString()),
        IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture) ?? "NULL",
        _ => Quote(Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty),
    };

    public static string Quote(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return string.Concat("'", value.Replace("'", "''", StringComparison.Ordinal), "'");
    }

    private static string FormatBoolean(bool value, DatabaseEngine engine)
    {
        if (engine == DatabaseEngine.PostgreSql)
        {
            return value ? "TRUE" : "FALSE";
        }

        return value ? "1" : "0";
    }

    private static string FormatBytes(byte[] bytes, DatabaseEngine engine)
    {
        var hex = Convert.ToHexString(bytes);
        return engine == DatabaseEngine.SqlServer ? "0x" + hex : "X'" + hex + "'";
    }
}

