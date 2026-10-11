using System.Drawing.Drawing2D;
using DbMigration.App.Theme;

namespace DbMigration.App.Controls;

internal static class ThemeRenderer
{
    public const float Radius = 6f;

    public static GraphicsPath RoundedRect(RectangleF bounds, float radius)
    {
        var path = new GraphicsPath();
        float r = Math.Max(0f, Math.Min(radius, Math.Min(bounds.Width, bounds.Height) / 2f));
        if (r <= 0.5f)
        {
            path.AddRectangle(bounds);
            return path;
        }

        float d = r * 2f;
        path.AddArc(bounds.Left, bounds.Top, d, d, 180f, 90f);
        path.AddArc(bounds.Right - d, bounds.Top, d, d, 270f, 90f);
        path.AddArc(bounds.Right - d, bounds.Bottom - d, d, d, 0f, 90f);
        path.AddArc(bounds.Left, bounds.Bottom - d, d, d, 90f, 90f);
        path.CloseFigure();
        return path;
    }

    public static void FillRounded(Graphics g, RectangleF bounds, float radius, Color fill)
    {
        using var path = RoundedRect(bounds, radius);
        using var brush = new SolidBrush(fill);
        g.FillPath(brush, path);
    }

    public static void DrawRoundedBorder(Graphics g, RectangleF bounds, float radius, Color color, float width = 1f)
    {
        var inset = new RectangleF(
            bounds.X + width / 2f,
            bounds.Y + width / 2f,
            Math.Max(0f, bounds.Width - width),
            Math.Max(0f, bounds.Height - width));
        using var path = RoundedRect(inset, radius);
        using var pen = new Pen(color, width) { Alignment = PenAlignment.Center };
        g.DrawPath(pen, path);
    }

    public static void DrawChevron(Graphics g, RectangleF bounds, Color color, bool down, float width = 1.6f)
    {
        g.SmoothingMode = SmoothingMode.AntiAlias;
        float cx = bounds.X + bounds.Width / 2f;
        float cy = bounds.Y + bounds.Height / 2f;
        float w = 4.5f;
        float h = down ? 2.6f : -2.6f;
        using var pen = new Pen(color, width) { StartCap = LineCap.Round, EndCap = LineCap.Round };
        g.DrawLine(pen, cx - w, cy - h, cx, cy + h);
        g.DrawLine(pen, cx, cy + h, cx + w, cy - h);
    }

    public static void DrawCheck(Graphics g, RectangleF bounds, Color color, float width = 1.8f)
    {
        g.SmoothingMode = SmoothingMode.AntiAlias;
        float cx = bounds.X + bounds.Width / 2f;
        float cy = bounds.Y + bounds.Height / 2f;
        using var pen = new Pen(color, width)
        {
            StartCap = LineCap.Round,
            EndCap = LineCap.Round,
            LineJoin = LineJoin.Round,
        };
        g.DrawLines(pen, new[]
        {
            new PointF(cx - 4.2f, cy + 0.2f),
            new PointF(cx - 1.2f, cy + 3.2f),
            new PointF(cx + 4.4f, cy - 3.2f),
        });
    }

    public static void DrawTriangle(Graphics g, PointF center, Color color, bool expanded, float size = 4.5f)
    {
        g.SmoothingMode = SmoothingMode.AntiAlias;
        PointF[] points = expanded
            ? new[]
            {
                new PointF(center.X - size, center.Y - size * 0.6f),
                new PointF(center.X + size, center.Y - size * 0.6f),
                new PointF(center.X, center.Y + size * 0.8f),
            }
            : new[]
            {
                new PointF(center.X - size * 0.6f, center.Y - size),
                new PointF(center.X - size * 0.6f, center.Y + size),
                new PointF(center.X + size * 0.8f, center.Y),
            };
        using var brush = new SolidBrush(color);
        g.FillPolygon(brush, points);
    }
}

