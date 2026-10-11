using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;

namespace DbMigration.App.Controls;

public sealed class SectionHeader : Control
{
    private string _title = string.Empty;
    private string _description = string.Empty;

    public SectionHeader()
    {
        SetStyle(
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.UserPaint |
            ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.ResizeRedraw |
            ControlStyles.SupportsTransparentBackColor,
            true);

        BackColor = Color.Transparent;
        Height = 52;
        Font = UiFonts.Section;
    }

    [DefaultValue("")]
    public string Title
    {
        get => _title;
        set
        {
            _title = value ?? string.Empty;
            Invalidate();
        }
    }

    [DefaultValue("")]
    public string Description
    {
        get => _description;
        set
        {
            _description = value ?? string.Empty;
            Invalidate();
        }
    }

    protected override void OnPaintBackground(PaintEventArgs pevent)
    {
        if (BackColor == Color.Transparent && Parent is { } parent)
        {
            var state = pevent.Graphics.Save();
            try
            {
                pevent.Graphics.TranslateTransform(-Left, -Top);
                var parentArgs = new PaintEventArgs(pevent.Graphics, new Rectangle(Left, Top, Width, Height));
                InvokePaintBackground(parent, parentArgs);
                InvokePaint(parent, parentArgs);
            }
            finally
            {
                pevent.Graphics.Restore(state);
            }

            return;
        }

        base.OnPaintBackground(pevent);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;

        using (var accent = new SolidBrush(OpencodeTheme.Primary))
        {
            g.FillRectangle(accent, 0, 6, 4, 22);
        }

        var titleRect = new Rectangle(14, 4, Math.Max(1, Width - 24), 26);
        TextRenderer.DrawText(
            g,
            _title,
            Font,
            titleRect,
            OpencodeTheme.Text,
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix | TextFormatFlags.EndEllipsis);

        if (_description.Length > 0)
        {
            var descRect = new Rectangle(14, 28, Math.Max(1, Width - 24), 18);
            TextRenderer.DrawText(
                g,
                _description,
                UiFonts.BodySmall,
                descRect,
                OpencodeTheme.TextMuted,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix | TextFormatFlags.EndEllipsis);
        }

        using var pen = new Pen(OpencodeTheme.BorderSubtle);
        g.DrawLine(pen, 0, Height - 1, Width, Height - 1);
    }
}
