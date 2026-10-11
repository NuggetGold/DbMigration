using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;

namespace DbMigration.App.Controls;

public sealed class Card : Panel
{
    private string _caption = string.Empty;

    public Card()
    {
        SetStyle(
            ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.UserPaint,
            true);

        BackColor = OpencodeTheme.BackgroundPanel;
        Padding = new Padding(16);
        Font = UiFonts.Body;
        ForeColor = OpencodeTheme.Text;
    }

    [DefaultValue("")]
    public string Caption
    {
        get => _caption;
        set
        {
            _caption = value ?? string.Empty;
            Invalidate();
        }
    }

    [DefaultValue(false)]
    public bool Highlight { get; set; }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        var bounds = new RectangleF(0.5f, 0.5f, Width - 1f, Height - 1f);
        ThemeRenderer.FillRounded(g, bounds, 8f, OpencodeTheme.BackgroundPanel);
        ThemeRenderer.DrawRoundedBorder(
            g,
            bounds,
            8f,
            Highlight ? OpencodeTheme.WithAlpha(OpencodeTheme.Primary, 120) : OpencodeTheme.BorderSubtle);

        if (_caption.Length > 0)
        {
            var rect = new Rectangle(16, 10, Math.Max(1, Width - 32), 22);
            TextRenderer.DrawText(
                g,
                _caption,
                UiFonts.Section,
                rect,
                OpencodeTheme.Primary,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
        }

        base.OnPaint(e);
    }
}
