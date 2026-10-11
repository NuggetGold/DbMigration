using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;

namespace DbMigration.App.Controls;

public sealed class ThemedLink : Control
{
    private const float TwoPi = MathF.PI * 2f;

    private string _url = string.Empty;
    private bool _hover;
    private bool _animate;
    private bool _subscribed;
    private Color _pulseFrom = OpencodeTheme.PurpleDark;
    private Color _pulseTo = OpencodeTheme.PurpleLight;
    private float _pulsePeriod = 3.2f;

    public ThemedLink()
    {
        SetStyle(
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.UserPaint |
            ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.ResizeRedraw |
            ControlStyles.SupportsTransparentBackColor,
            true);

        BackColor = Color.Transparent;
        ForeColor = OpencodeTheme.Info;
        Font = UiFonts.BodySmall;
        Cursor = Cursors.Hand;
        AutoSize = true;
        Height = 20;
    }

    [DefaultValue("")]
    public string Url
    {
        get => _url;
        set => _url = value ?? string.Empty;
    }

    [DefaultValue(false)]
    public bool Animate
    {
        get => _animate;
        set
        {
            if (_animate == value)
            {
                return;
            }

            _animate = value;
            SyncSubscription();
            Invalidate();
        }
    }

    [DefaultValue(typeof(Color), "107, 78, 168")]
    public Color PulseFrom
    {
        get => _pulseFrom;
        set
        {
            _pulseFrom = value;
            Invalidate();
        }
    }

    [DefaultValue(typeof(Color), "201, 169, 245")]
    public Color PulseTo
    {
        get => _pulseTo;
        set
        {
            _pulseTo = value;
            Invalidate();
        }
    }

    [DefaultValue(3.2f)]
    public float PulsePeriod
    {
        get => _pulsePeriod;
        set => _pulsePeriod = Math.Clamp(value, 0.4f, 30f);
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        SyncSubscription();
    }

    protected override void OnHandleDestroyed(EventArgs e)
    {
        Unsubscribe();
        base.OnHandleDestroyed(e);
    }

    private void SyncSubscription()
    {
        if (_animate && IsHandleCreated && !_subscribed)
        {
            AnimationClock.Instance.Tick += OnTick;
            _subscribed = true;
        }
        else if ((!_animate || !IsHandleCreated) && _subscribed)
        {
            Unsubscribe();
        }
    }

    private void Unsubscribe()
    {
        if (!_subscribed)
        {
            return;
        }

        AnimationClock.Instance.Tick -= OnTick;
        _subscribed = false;
    }

    private void OnTick(object? sender, EventArgs e)
    {
        if (Visible)
        {
            Invalidate();
        }
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
        Invalidate();
    }

    protected override void OnClick(EventArgs e)
    {
        base.OnClick(e);
        Open();
    }

    public void Open()
    {
        if (_url.Length == 0)
        {
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo(_url) { UseShellExecute = true });
        }
        catch (Exception)
        {
        }
    }

    private Color CurrentColor()
    {
        Color color;
        if (_animate)
        {
            double t = AnimationClock.Instance.Time;
            float wave = 0.5f * (1f + MathF.Sin((float)(TwoPi * t / _pulsePeriod)));
            color = OpencodeTheme.Lerp(_pulseFrom, _pulseTo, wave);
        }
        else
        {
            color = ForeColor;
        }

        return _hover ? OpencodeTheme.Lerp(color, Color.White, 0.35) : color;
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
        Color color = CurrentColor();
        var rect = new Rectangle(0, 0, Math.Max(1, Width), Height);
        TextRenderer.DrawText(
            g,
            Text ?? string.Empty,
            Font,
            rect,
            color,
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);

        if (_hover)
        {
            Size size = TextRenderer.MeasureText(g, Text ?? string.Empty, Font, Size.Empty, TextFormatFlags.NoPadding);
            int y = (Height + Font.Height) / 2;
            using var pen = new Pen(color);
            g.DrawLine(pen, 0, y, size.Width, y);
        }
    }

    public override Size GetPreferredSize(Size proposedSize)
    {
        Size size = TextRenderer.MeasureText(Text ?? string.Empty, Font, Size.Empty, TextFormatFlags.NoPadding);
        return new Size(size.Width + 2, Math.Max(18, size.Height + 2));
    }
}
