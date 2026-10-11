using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;

namespace DbMigration.App.Controls;

[DefaultProperty(nameof(Text))]
public sealed class GlowLabel : Control
{
    private const float TwoPi = MathF.PI * 2f;

    private float _pulsePeriod = 2.6f;
    private float _minIntensity = 0.18f;
    private float _maxIntensity = 0.85f;
    private int _glowRadius = 6;
    private int _glowPasses = 3;

    public GlowLabel()
    {
        SetStyle(
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.UserPaint |
            ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.ResizeRedraw |
            ControlStyles.SupportsTransparentBackColor,
            true);

        BackColor = Color.Transparent;
        ForeColor = OpencodeTheme.Text;
        Font = AppFonts.Display(14f);
        AutoSize = true;
        Size = new Size(160, 30);
    }

    [Category("Appearance")]
    [DefaultValue(typeof(Color), "250, 178, 131")]
    public Color GlowColor { get; set; } = OpencodeTheme.Primary;

    [Category("Appearance")]
    [DefaultValue(typeof(Color), "255, 192, 159")]
    public Color GlowHighlight { get; set; } = OpencodeTheme.PrimaryBright;

    [Category("Behavior")]
    [DefaultValue(2.6f)]
    public float PulsePeriod
    {
        get => _pulsePeriod;
        set => _pulsePeriod = Math.Clamp(value, 0.2f, 30f);
    }

    [Category("Behavior")]
    [DefaultValue(0.18f)]
    public float MinIntensity
    {
        get => _minIntensity;
        set => _minIntensity = Math.Clamp(value, 0f, 1f);
    }

    [Category("Behavior")]
    [DefaultValue(0.85f)]
    public float MaxIntensity
    {
        get => _maxIntensity;
        set => _maxIntensity = Math.Clamp(value, 0f, 1f);
    }

    [Category("Appearance")]
    [DefaultValue(6)]
    public int GlowRadius
    {
        get => _glowRadius;
        set
        {
            _glowRadius = Math.Clamp(value, 0, 32);
            OnVisualPropertyChanged();
        }
    }

    [Category("Appearance")]
    [DefaultValue(3)]
    public int GlowPasses
    {
        get => _glowPasses;
        set => _glowPasses = Math.Clamp(value, 1, 8);
    }

    [Category("Behavior")]
    [DefaultValue(true)]
    public bool Animate { get; set; } = true;

    [Category("Behavior")]
    [DefaultValue(0f)]
    public float PhaseOffset { get; set; }

    [Category("Appearance")]
    [DefaultValue(false)]
    public bool ShineEnabled { get; set; }

    [Category("Appearance")]
    [DefaultValue(140f)]
    public float ShineSpeed { get; set; } = 140f;

    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public float Intensity { get; private set; }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        AnimationClock.Instance.Tick += OnClockTick;
    }

    protected override void OnHandleDestroyed(EventArgs e)
    {
        AnimationClock.Instance.Tick -= OnClockTick;
        base.OnHandleDestroyed(e);
    }

    private void OnClockTick(object? sender, EventArgs e)
    {
        if (Animate && IsHandleCreated && Visible)
        {
            Invalidate();
        }
    }

    protected override void OnTextChanged(EventArgs e)
    {
        base.OnTextChanged(e);
        OnVisualPropertyChanged();
    }

    protected override void OnFontChanged(EventArgs e)
    {
        base.OnFontChanged(e);
        OnVisualPropertyChanged();
    }

    private void OnVisualPropertyChanged()
    {
        if (AutoSize)
        {
            Size = GetPreferredSize(Size.Empty);
        }

        Invalidate();
    }

    protected override void OnPaintBackground(PaintEventArgs pevent)
    {
        if (BackColor == Color.Transparent && Parent is { } parent)
        {
            var state = pevent.Graphics.Save();
            try
            {
                pevent.Graphics.TranslateTransform(-Left, -Top);
                var parentRect = new Rectangle(Left, Top, Width, Height);
                var parentArgs = new PaintEventArgs(pevent.Graphics, parentRect);
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

        if (string.IsNullOrEmpty(Text) || Font is null)
        {
            Intensity = 0;
            return;
        }

        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;

        double t = AnimationClock.Instance.Time + PhaseOffset;
        float wave = 0.5f * (1f + MathF.Sin((float)(TwoPi * t / _pulsePeriod)));
        Intensity = Animate ? _minIntensity + (_maxIntensity - _minIntensity) * wave : _minIntensity;

        float emPixels = Font.SizeInPoints * g.DpiY / 72f;
        using var format = new StringFormat(StringFormatFlags.NoWrap)
        {
            Trimming = StringTrimming.None,
        };

        using var path = new GraphicsPath();
        path.AddString(Text, Font.FontFamily, (int)Font.Style, emPixels, PointF.Empty, format);
        var bounds = path.GetBounds();

        float dx = (Width - bounds.Width) / 2f - bounds.X;
        float dy = (Height - bounds.Height) / 2f - bounds.Y;
        using (var transform = new Matrix())
        {
            transform.Translate(dx, dy);
            path.Transform(transform);
        }

        var placed = path.GetBounds();

        if (_glowRadius > 0 && Intensity > 0.01f)
        {
            for (int pass = 0; pass < _glowPasses; pass++)
            {
                float spread = _glowRadius * 2f * (pass + 1) / _glowPasses;
                float falloff = 1f - (float)pass / _glowPasses;
                int alpha = (int)Math.Clamp(255f * Intensity * 0.16f * falloff, 0, 255);
                if (alpha <= 0)
                {
                    continue;
                }

                using var pen = new Pen(OpencodeTheme.WithAlpha(GlowColor, alpha), Math.Max(1f, spread))
                {
                    LineJoin = LineJoin.Round,
                    StartCap = LineCap.Round,
                    EndCap = LineCap.Round,
                };
                g.DrawPath(pen, path);
            }
        }

        var coreColor = OpencodeTheme.Lerp(ForeColor, GlowHighlight, Intensity * 0.65);
        using (var coreBrush = new SolidBrush(coreColor))
        {
            g.FillPath(coreBrush, path);
        }

        if (ShineEnabled && placed.Width > 1f)
        {
            DrawShine(g, path, placed, t);
        }
    }

    private void DrawShine(Graphics g, GraphicsPath path, RectangleF placed, double t)
    {
        float band = Math.Max(24f, placed.Width * 0.32f);
        float travel = placed.Width + band * 2f;
        float x = placed.X - band + (float)((t * ShineSpeed) % travel);

        var clip = g.Clip;
        try
        {
            g.SetClip(path, CombineMode.Replace);
            var rect = new RectangleF(x, placed.Y - 2f, band, placed.Height + 4f);
            using var gradient = new LinearGradientBrush(
                rect,
                Color.Transparent,
                Color.Transparent,
                LinearGradientMode.Horizontal);
            var blend = new ColorBlend(3)
            {
                Colors = new[] { Color.Transparent, OpencodeTheme.WithAlpha(Color.White, 110), Color.Transparent },
                Positions = new[] { 0f, 0.5f, 1f },
            };
            gradient.InterpolationColors = blend;
            g.FillRectangle(gradient, rect);
        }
        finally
        {
            g.Clip = clip;
        }
    }

    public override Size GetPreferredSize(Size proposedSize)
    {
        if (string.IsNullOrEmpty(Text) || Font is null)
        {
            return Size.Empty;
        }

        using var g = CreateGraphics();
        float emPixels = Font.SizeInPoints * g.DpiY / 72f;
        using var path = new GraphicsPath();
        using var format = new StringFormat(StringFormatFlags.NoWrap);
        path.AddString(Text, Font.FontFamily, (int)Font.Style, emPixels, PointF.Empty, format);
        var bounds = path.GetBounds();
        int pad = _glowRadius + 3;
        return new Size(
            (int)Math.Ceiling(bounds.Width) + pad * 2,
            (int)Math.Ceiling(bounds.Height) + pad * 2);
    }
}

