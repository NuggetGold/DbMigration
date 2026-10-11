using System.ComponentModel;
using System.Drawing.Text;
using DbMigration.App.Theme;

namespace DbMigration.App.Controls;

public sealed class HintStrip : Control
{
    private readonly List<Segment> _segments = new();
    private string _markup = string.Empty;

    public HintStrip()
    {
        SetStyle(
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.UserPaint |
            ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.ResizeRedraw,
            true);

        TabStop = false;
        BackColor = OpencodeTheme.BackgroundPanel;
        ForeColor = OpencodeTheme.TextMuted;
        Font = UiFonts.BodySmall;
        Height = 32;
        Dock = DockStyle.Top;
    }

    [DefaultValue("")]
    public string Markup
    {
        get => _markup;
        set
        {
            _markup = value ?? string.Empty;
            Parse();
            Invalidate();
        }
    }

    public void SetSegments(params (string Text, Color Color)[] segments)
    {
        _segments.Clear();
        _markup = string.Empty;
        if (segments is not null)
        {
            foreach (var (text, color) in segments)
            {
                if (!string.IsNullOrEmpty(text))
                {
                    _segments.Add(new Segment(text, color));
                }
            }
        }

        Invalidate();
    }

    public static Color ColorForToken(string token) => token.ToLowerInvariant() switch
    {
        "primary" => OpencodeTheme.Primary,
        "primarybright" => OpencodeTheme.PrimaryBright,
        "secondary" => OpencodeTheme.Secondary,
        "accent" => OpencodeTheme.Accent,
        "success" => OpencodeTheme.Success,
        "warning" => OpencodeTheme.Warning,
        "error" => OpencodeTheme.Error,
        "info" => OpencodeTheme.Info,
        "muted" => OpencodeTheme.TextMuted,
        _ => OpencodeTheme.Text,
    };

    private void Parse()
    {
        _segments.Clear();
        var text = _markup;
        int i = 0;
        while (i < text.Length)
        {
            int open = text.IndexOf('<', i);
            if (open < 0)
            {
                AddPlain(text[i..]);
                break;
            }

            if (open > i)
            {
                AddPlain(text[i..open]);
            }

            int close = text.IndexOf('>', open);
            if (close < 0)
            {
                AddPlain(text[open..]);
                break;
            }

            string tag = text[(open + 1)..close].Trim();
            string endTag = "</" + tag + ">";
            int end = text.IndexOf(endTag, close + 1, StringComparison.OrdinalIgnoreCase);
            if (tag.Length == 0 || end < 0)
            {
                AddPlain(text[open..(close + 1)]);
                i = close + 1;
                continue;
            }

            var color = ColorForToken(tag);
            AddPlain(text[(close + 1)..end], color);
            i = end + endTag.Length;
        }
    }

    private void AddPlain(string text, Color? color = null)
    {
        if (!string.IsNullOrEmpty(text))
        {
            _segments.Add(new Segment(text, color ?? ForeColor));
        }
    }

    protected override void OnFontChanged(EventArgs e)
    {
        base.OnFontChanged(e);
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var g = e.Graphics;
        g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

        using var fill = new SolidBrush(BackColor);
        g.FillRectangle(fill, ClientRectangle);

        const TextFormatFlags flags = TextFormatFlags.NoPadding | TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix;
        int x = 16;
        int y = (Height - Font.Height) / 2;
        foreach (var segment in _segments)
        {
            var size = TextRenderer.MeasureText(g, segment.Text, Font, new Size(int.MaxValue, Height), flags);
            var bounds = new Rectangle(x, y, size.Width, Font.Height);
            TextRenderer.DrawText(g, segment.Text, Font, bounds, segment.Color, flags);
            x += size.Width;
        }
    }

    private readonly record struct Segment(string Text, Color Color);
}

