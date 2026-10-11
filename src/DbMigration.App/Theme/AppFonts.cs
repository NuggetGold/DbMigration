using System.Drawing;
using System.Drawing.Text;

namespace DbMigration.App.Theme;

public static class AppFonts
{
    private static readonly HashSet<string> Installed =
        new(StringComparer.OrdinalIgnoreCase);

    static AppFonts()
    {
        try
        {
            using var collection = new InstalledFontCollection();
            foreach (var family in collection.Families)
            {
                Installed.Add(family.Name);
            }
        }
        catch
        {
        }
    }

    public static Font Ui(float size = 9.75f, FontStyle style = FontStyle.Regular) =>
        Create(new[] { "Segoe UI Variable Text", "Segoe UI", "Tahoma" }, size, style);

    public static Font Mono(float size = 9.75f, FontStyle style = FontStyle.Regular) =>
        Create(new[] { "Cascadia Mono", "Cascadia Code", "Consolas", "Courier New" }, size, style);

    public static Font Display(float size = 14f, FontStyle style = FontStyle.Bold) =>
        Create(new[] { "Bahnschrift SemiBold", "Bahnschrift", "Segoe UI Semibold", "Segoe UI" }, size, style);

    private static Font Create(IEnumerable<string> families, float size, FontStyle style)
    {
        foreach (var family in families)
        {
            if (family.Equals("Segoe UI", StringComparison.OrdinalIgnoreCase) || Installed.Contains(family))
            {
                try
                {
                    return new Font(family, size, style, GraphicsUnit.Point);
                }
                catch
                {
                }
            }
        }

        return new Font(FontFamily.GenericSansSerif, size, style, GraphicsUnit.Point);
    }
}

