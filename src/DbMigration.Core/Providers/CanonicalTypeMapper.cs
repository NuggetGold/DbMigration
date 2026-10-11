using System.Globalization;

namespace DbMigration.Core.Providers;

internal static class CanonicalTypeMapper
{
    public static CanonicalType Infer(string? nativeType)
    {
        if (string.IsNullOrWhiteSpace(nativeType))
        {
            return CanonicalType.Unknown;
        }

        var type = nativeType.Trim().ToLowerInvariant();
        var core = StripLengthSpecifier(type);
        var full = type;

        if (Contains(full, "json"))
        {
            return CanonicalType.Json;
        }

        if (Contains(full, "uuid") || Contains(full, "uniqueidentifier") || Contains(full, "guid"))
        {
            return CanonicalType.Uuid;
        }

        if (Contains(full, "bytea") || Contains(full, "blob") || Contains(full, "binary")
            || Contains(full, "image") || Contains(full, "raw"))
        {
            return CanonicalType.Blob;
        }

        if (Contains(full, "bit varying") || Contains(full, "varbit"))
        {
            return CanonicalType.Blob;
        }

        if (Contains(core, "interval"))
        {
            return CanonicalType.Time;
        }

        if (Contains(full, "timestamp") || Contains(full, "datetime") || Contains(full, "smalldatetime"))
        {
            return CanonicalType.DateTime;
        }

        if (Contains(core, "date"))
        {
            return CanonicalType.Date;
        }

        if (Contains(core, "time"))
        {
            return CanonicalType.Time;
        }

        if (Contains(core, "bool") || core == "bit")
        {
            return CanonicalType.Boolean;
        }

        if (Contains(core, "tinyint") && Contains(full, "(1)"))
        {
            return CanonicalType.Boolean;
        }

        if (Contains(core, "bigint") || Contains(core, "int8") || Contains(core, "serial8"))
        {
            return CanonicalType.BigInt;
        }

        if (Contains(core, "smallint") || Contains(core, "mediumint") || Contains(core, "int2")
            || Contains(core, "int") || Contains(core, "serial"))
        {
            return CanonicalType.Integer;
        }

        if (Contains(core, "decimal") || Contains(core, "numeric") || Contains(core, "money")
            || Contains(core, "number") || Contains(core, "dec"))
        {
            return CanonicalType.Decimal;
        }

        if (Contains(core, "double") || Contains(core, "real") || Contains(core, "float"))
        {
            return CanonicalType.Real;
        }

        if (Contains(core, "char") || Contains(core, "text") || Contains(core, "clob")
            || Contains(core, "string") || Contains(core, "enum") || Contains(core, "set")
            || Contains(core, "name"))
        {
            return CanonicalType.Text;
        }

        return CanonicalType.Unknown;
    }

    public static int? ParseDeclaredLength(string? declaredType)
    {
        if (string.IsNullOrWhiteSpace(declaredType))
        {
            return null;
        }

        var open = declaredType.IndexOf('(', StringComparison.Ordinal);
        if (open < 0)
        {
            return null;
        }

        var close = declaredType.IndexOf(')', open + 1);
        var end = close < 0 ? declaredType.Length : close;
        var inner = declaredType.AsSpan((open + 1), end - open - 1);
        var comma = inner.IndexOf(',');
        if (comma >= 0)
        {
            inner = inner[..comma];
        }

        return int.TryParse(inner.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var length)
            ? length
            : null;
    }

    private static string StripLengthSpecifier(string type)
    {
        var open = type.IndexOf('(', StringComparison.Ordinal);
        return open < 0 ? type : type[..open];
    }

    private static bool Contains(string haystack, string needle) =>
        haystack.Contains(needle, StringComparison.Ordinal);
}

