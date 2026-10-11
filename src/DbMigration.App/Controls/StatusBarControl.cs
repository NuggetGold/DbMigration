using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;

namespace DbMigration.App.Controls;

public sealed class StatusBarControl : Control
{
    private string _status = "Готово";
    private string _leftInfo = string.Empty;
    private string _rightInfo = string.Empty;
    private bool _busy;

    public StatusBarControl()
    {
        SetStyle(
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.UserPaint |
            ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.ResizeRedraw,
            true);

        Dock = DockStyle.Bottom;
        Height = 30;
        BackColor = OpencodeTheme.BackgroundPanel;
        ForeColor = OpencodeTheme.TextMuted;
        Font = UiFonts.BodySmall;
    }

    [DefaultValue("Готово")]
    public string Status
    {
        get => _status;
        set
        {
            _status = value ?? string.Empty;
            Invalidate();
        }
    }

    [DefaultValue("")]
    public string LeftInfo
    {
        get => _leftInfo;
        set
        {
            _leftInfo = value ?? string.Empty;
            Invalidate();
        }
    }

    [DefaultValue("")]
    public string RightInfo
    {
        get => _rightInfo;
        set
        {
            _rightInfo = value ?? string.Empty;
            Invalidate();
        }
    }

    [DefaultValue(false)]
    public bool Busy
    {
        get => _busy;
        set
        {
            if (_busy == value)
            {
                return;
            }

            _busy = value;
            if (value)
            {
                AnimationClock.Instance.Tick += OnTick;
            }
            else
            {
                AnimationClock.Instance.Tick -= OnTick;
            }

            Invalidate();
        }
    }

    protected override void OnHandleDestroyed(EventArgs e)
    {
        AnimationClock.Instance.Tick -= OnTick;
        base.OnHandleDestroyed(e);
    }

    private void OnTick(object? sender, EventArgs e)
    {
        if (_busy && Visible)
        {
            Invalidate();
        }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var g = e.Graphics;
        using (var brush = new SolidBrush(OpencodeTheme.BackgroundPanel))
        {
            g.FillRectangle(brush, ClientRectangle);
        }

        using (var pen = new Pen(OpencodeTheme.BorderSubtle))
        {
            g.DrawLine(pen, 0, 0, Width, 0);
        }

        g.SmoothingMode = SmoothingMode.AntiAlias;
        int x = 14;
        var dot = new RectangleF(x, Height / 2f - 4f, 8f, 8f);
        Color dotColor = _busy ? OpencodeTheme.Primary : OpencodeTheme.Success;
        int alpha = _busy
            ? (int)(120 + 135 * (0.5 + 0.5 * Math.Sin(AnimationClock.Instance.Time * 4)))
            : 255;
        using (var brush = new SolidBrush(OpencodeTheme.WithAlpha(dotColor, alpha)))
        {
            g.FillEllipse(brush, dot);
        }

        x += 18;
        TextRenderer.DrawText(
            g,
            _status,
            Font,
            new Rectangle(x, 0, Math.Max(1, Width - x - 300), Height),
            _busy ? OpencodeTheme.Primary : OpencodeTheme.TextMuted,
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix | TextFormatFlags.EndEllipsis);

        if (_leftInfo.Length > 0)
        {
            int infoX = x + TextRenderer.MeasureText(g, _status, Font, Size.Empty, TextFormatFlags.NoPadding).Width + 24;
            TextRenderer.DrawText(
                g,
                _leftInfo,
                Font,
                new Rectangle(infoX, 0, Math.Max(1, Width - infoX - 300), Height),
                OpencodeTheme.Info,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix | TextFormatFlags.EndEllipsis);
        }

        if (_rightInfo.Length > 0)
        {
            var size = TextRenderer.MeasureText(g, _rightInfo, Font, Size.Empty, TextFormatFlags.NoPadding);
            TextRenderer.DrawText(
                g,
                _rightInfo,
                Font,
                new Rectangle(Width - size.Width - 14, 0, size.Width + 2, Height),
                OpencodeTheme.TextMuted,
                TextFormatFlags.Right | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
        }
    }
}
