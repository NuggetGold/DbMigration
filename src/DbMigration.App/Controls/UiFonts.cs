using DbMigration.App.Theme;

namespace DbMigration.App.Controls;

internal static class UiFonts
{
    public static readonly Font Body = AppFonts.Ui(9.75f);

    public static readonly Font BodySmall = AppFonts.Ui(8.75f);

    public static readonly Font BodyBold = AppFonts.Ui(9.75f, FontStyle.Bold);

    public static readonly Font Caption = AppFonts.Ui(8.25f);

    public static readonly Font Mono = AppFonts.Mono(9.5f);

    public static readonly Font MonoSmall = AppFonts.Mono(8.5f);

    public static readonly Font Section = AppFonts.Ui(11f, FontStyle.Bold);

    public static readonly Font Display = AppFonts.Display(14f);

    public static readonly Font DisplayLarge = AppFonts.Display(22f);

    public static readonly Font DisplayHuge = AppFonts.Display(30f);
}

