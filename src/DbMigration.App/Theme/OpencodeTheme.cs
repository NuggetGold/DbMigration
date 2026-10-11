using System.Drawing;
using DbMigration.Core.Models;

namespace DbMigration.App.Theme;

public static class OpencodeTheme
{
    public static readonly Color Background = Color.FromArgb(0x0A, 0x0A, 0x0A);
    public static readonly Color BackgroundPanel = Color.FromArgb(0x14, 0x14, 0x14);
    public static readonly Color BackgroundElement = Color.FromArgb(0x1E, 0x1E, 0x1E);
    public static readonly Color BackgroundElementHover = Color.FromArgb(0x28, 0x28, 0x28);
    public static readonly Color BackgroundElementActive = Color.FromArgb(0x32, 0x32, 0x32);
    public static readonly Color BorderSubtle = Color.FromArgb(0x3C, 0x3C, 0x3C);
    public static readonly Color Border = Color.FromArgb(0x48, 0x48, 0x48);
    public static readonly Color BorderActive = Color.FromArgb(0x60, 0x60, 0x60);
    public static readonly Color TextMuted = Color.FromArgb(0x80, 0x80, 0x80);
    public static readonly Color Text = Color.FromArgb(0xEE, 0xEE, 0xEE);

    public static readonly Color Primary = Color.FromArgb(0xFA, 0xB2, 0x83);
    public static readonly Color PrimaryBright = Color.FromArgb(0xFF, 0xC0, 0x9F);
    public static readonly Color Secondary = Color.FromArgb(0x5C, 0x9C, 0xF5);
    public static readonly Color Accent = Color.FromArgb(0x9D, 0x7C, 0xD8);
    public static readonly Color PurpleLight = Color.FromArgb(0xC9, 0xA9, 0xF5);
    public static readonly Color PurpleDark = Color.FromArgb(0x6B, 0x4E, 0xA8);
    public static readonly Color SkyBlue = Color.FromArgb(0x6C, 0xB6, 0xF5);
    public static readonly Color Error = Color.FromArgb(0xE0, 0x6C, 0x75);
    public static readonly Color Warning = Color.FromArgb(0xF5, 0xA7, 0x42);
    public static readonly Color Success = Color.FromArgb(0x7F, 0xD8, 0x8F);
    public static readonly Color Info = Color.FromArgb(0x56, 0xB6, 0xC2);

    public static Color ForLogLevel(LogLevel level) => level switch
    {
        LogLevel.Trace => TextMuted,
        LogLevel.Info => Info,
        LogLevel.Success => Success,
        LogLevel.Warning => Warning,
        LogLevel.Error => Error,
        _ => Text,
    };

    public static Color Lerp(Color from, Color to, double amount)
    {
        double t = Math.Clamp(amount, 0, 1);
        return Color.FromArgb(
            (int)Math.Round(from.A + (to.A - from.A) * t),
            (int)Math.Round(from.R + (to.R - from.R) * t),
            (int)Math.Round(from.G + (to.G - from.G) * t),
            (int)Math.Round(from.B + (to.B - from.B) * t));
    }

    public static Color WithAlpha(Color color, int alpha) =>
        Color.FromArgb(Math.Clamp(alpha, 0, 255), color.R, color.G, color.B);
}

