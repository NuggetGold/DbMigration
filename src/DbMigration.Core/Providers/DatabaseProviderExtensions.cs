using System.Text;

namespace DbMigration.Core.Providers;

public static class DatabaseProviderExtensions
{
    public static string QuoteQualifiedName(this IDatabaseProvider provider, string qualifiedName)
    {
        ArgumentNullException.ThrowIfNull(provider);
        ArgumentException.ThrowIfNullOrWhiteSpace(qualifiedName);

        var parts = qualifiedName.Split('.');
        if (parts.Length == 1)
        {
            return provider.QuoteIdentifier(parts[0]);
        }

        var builder = new StringBuilder();
        for (var i = 0; i < parts.Length; i++)
        {
            if (i > 0)
            {
                builder.Append('.');
            }

            builder.Append(provider.QuoteIdentifier(parts[i]));
        }

        return builder.ToString();
    }
}

