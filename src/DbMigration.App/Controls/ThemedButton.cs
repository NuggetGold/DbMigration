using System.ComponentModel;
using System.Drawing.Drawing2D;
using DbMigration.App.Theme;

namespace DbMigration.App.Controls;

public enum ButtonVariant
{
    Primary,

    Default,

    Ghost,

    Danger,
}

public sealed class ThemedButton : Control, IButtonControl
{
    private ButtonVariant _variant = ButtonVariant.Default;
    private bool _hover;
    private bool _pressed;
    private bool _active;
    private ContentAlignment _textAlign = ContentAlignment.MiddleCenter;

    public ThemedButton()
    {
        SetStyle(
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.UserPaint |
            ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.ResizeRedraw |
            ControlStyles.Selectable |
            ControlStyles.SupportsTransparentBackColor,
            true);

        BackColor = Color.Transparent;
        ForeColor = OpencodeTheme.Text;
        Font = UiFonts.Body;
        Size = new Size(112, 34);
        TabStop = true;
        Cursor = Cursors.Hand;
    }

    [DefaultValue(ButtonVariant.Default)]
    public ButtonVariant Variant
    {
        get => _variant;
        set
        {
            if (_variant != value)
            {
                _variant = value;
                Invalidate();
            }
        }
    }

    [DefaultValue(null)]
    public string? Glyph { get; set; }

    [DefaultValue(false)]
    public bool Active
    {
        get => _active;
        set
        {
            if (_active != value)
            {
                _active = value;
                Invalidate();
            }
        }
    }

    [DefaultValue(ContentAlignment.MiddleCenter)]
    public ContentAlignment TextAlign
    {
        get => _textAlign;
        set
        {
            if (_textAlign != value)
            {
                _textAlign = value;
                Invalidate();
            }
        }
    }

    [DefaultValue(DialogResult.None)]
    public DialogResult DialogResult { get; set; } = DialogResult.None;

    public void NotifyDefault(bool value)
    {
        if (value)
        {
            Invalidate();
        }
    }

    public void PerformClick()
    {
        if (Enabled)
        {
            OnClick(EventArgs.Empty);
        }
    }

    protected override void OnTextChanged(EventArgs e)
    {
        base.OnTextChanged(e);
        Invalidate();
    }

    protected override void OnMouseEnter(EventArgs e)
    {
        base.OnMouseEnter(e);
        _hover = true;
        Invalidate();
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        _hover = false;
        _pressed = false;
        Invalidate();
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (e.Button == MouseButtons.Left)
        {
            _pressed = true;
            Focus();
            Invalidate();
        }
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        if (e.Button == MouseButtons.Left)
        {
            _pressed = false;
            Invalidate();
        }
    }

    protected override void OnGotFocus(EventArgs e)
    {
        base.OnGotFocus(e);
        Invalidate();
    }

    protected override void OnLostFocus(EventArgs e)
    {
        base.OnLostFocus(e);
        Invalidate();
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.KeyCode is Keys.Space or Keys.Enter)
        {
            PerformClick();
            e.Handled = true;
        }
    }

    protected override bool IsInputKey(Keys keyData) => keyData is Keys.Space or Keys.Enter || base.IsInputKey(keyData);

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;

        var bounds = new RectangleF(0.5f, 0.5f, Width - 1f, Height - 1f);
        var (background, border, foreground) = ResolveColors();

        if (!Enabled)
        {
            background = OpencodeTheme.BackgroundElement;
            border = OpencodeTheme.BorderSubtle;
            foreground = OpencodeTheme.TextMuted;
        }

        if (background.A > 0)
        {
            ThemeRenderer.FillRounded(g, bounds, ThemeRenderer.Radius, background);
        }

        if (border.A > 0)
        {
            ThemeRenderer.DrawRoundedBorder(g, bounds, ThemeRenderer.Radius, border);
        }

        if (_active && _variant == ButtonVariant.Ghost)
        {
            using var pen = new Pen(OpencodeTheme.Primary, 3f);
            g.DrawLine(pen, 1.5f, 6f, 1.5f, Height - 6f);
        }

        if (Focused && Enabled)
        {
            ThemeRenderer.DrawRoundedBorder(g, bounds, ThemeRenderer.Radius, OpencodeTheme.Primary);
        }

        DrawContent(g, foreground);
    }

    private void DrawContent(Graphics g, Color foreground)
    {
        const TextFormatFlags baseFlags = TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis;

        string glyph = Glyph ?? string.Empty;
        string text = Text ?? string.Empty;
        int pad = 12;

        Size glyphSize = glyph.Length == 0 ? Size.Empty : TextRenderer.MeasureText(g, glyph, Font, Size.Empty, TextFormatFlags.NoPadding);
        Size textSize = TextRenderer.MeasureText(g, text, Font, new Size(Math.Max(1, Width - pad * 2), Height), baseFlags);
        int gap = glyph.Length > 0 && text.Length > 0 ? 8 : 0;
        int totalWidth = glyphSize.Width + gap + textSize.Width;

        int startX = _textAlign switch
        {
            ContentAlignment.MiddleLeft or ContentAlignment.TopLeft or ContentAlignment.BottomLeft => pad,
            ContentAlignment.MiddleRight or ContentAlignment.TopRight or ContentAlignment.BottomRight => Width - pad - totalWidth,
            _ => (Width - totalWidth) / 2,
        };

        int y = _textAlign switch
        {
            ContentAlignment.TopLeft or ContentAlignment.TopCenter or ContentAlignment.TopRight => pad,
            ContentAlignment.BottomLeft or ContentAlignment.BottomCenter or ContentAlignment.BottomRight => Height - pad - textSize.Height,
            _ => (Height - textSize.Height) / 2,
        };

        int x = Math.Max(pad, startX);
        if (glyph.Length > 0)
        {
            var glyphRect = new Rectangle(x, (Height - glyphSize.Height) / 2, glyphSize.Width, glyphSize.Height);
            TextRenderer.DrawText(g, glyph, Font, glyphRect, foreground, TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix);
            x += glyphSize.Width + gap;
        }

        if (text.Length > 0)
        {
            var textRect = new Rectangle(x, y, Math.Max(1, Width - x - pad), textSize.Height);
            TextRenderer.DrawText(g, text, Font, textRect, foreground, baseFlags);
        }
    }

    private (Color Background, Color Border, Color Foreground) ResolveColors()
    {
        return _variant switch
        {
            ButtonVariant.Primary => (
                _pressed ? OpencodeTheme.Lerp(OpencodeTheme.Primary, OpencodeTheme.Background, 0.18)
                    : _hover ? OpencodeTheme.PrimaryBright : OpencodeTheme.Primary,
                Color.Empty,
                OpencodeTheme.Background),

            ButtonVariant.Danger => (
                _pressed ? OpencodeTheme.Lerp(OpencodeTheme.Error, Color.Black, 0.18)
                    : _hover ? OpencodeTheme.Lerp(OpencodeTheme.Error, Color.White, 0.15) : OpencodeTheme.Error,
                Color.Empty,
                OpencodeTheme.Background),

            ButtonVariant.Ghost => (
                _pressed ? OpencodeTheme.BackgroundElementActive
                    : _hover ? OpencodeTheme.BackgroundElementHover
                    : _active ? OpencodeTheme.WithAlpha(OpencodeTheme.Primary, 40) : Color.Transparent,
                Color.Empty,
                _active ? OpencodeTheme.Primary : _hover ? OpencodeTheme.Text : OpencodeTheme.TextMuted),

            _ => (
                _pressed ? OpencodeTheme.BackgroundElementActive
                    : _hover ? OpencodeTheme.BackgroundElementHover : OpencodeTheme.BackgroundElement,
                _hover ? OpencodeTheme.BorderActive : OpencodeTheme.Border,
                OpencodeTheme.Text),
        };
    }
}

