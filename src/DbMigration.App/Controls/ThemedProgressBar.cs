using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;

namespace DbMigration.App.Controls;

public sealed class ThemedProgressBar : Control
{
    private int _minimum;
    private int _maximum = 100;
    private int _value;
    private bool _marquee;
    private float _marqueeOffset;

    public ThemedProgressBar()
    {
        SetStyle(
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.UserPaint |
            ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.ResizeRedraw,
            true);

        Height = 10;
        BackColor = OpencodeTheme.BackgroundElement;
    }

    [DefaultValue(0)]
    public int Minimum
    {
        get => _minimum;
        set
        {
            _minimum = value;
            Invalidate();
        }
    }

    [DefaultValue(100)]
    public int Maximum
    {
        get => _maximum;
        set
        {
            _maximum = value;
            Invalidate();
        }
    }

    [DefaultValue(0)]
    public int Value
    {
        get => _value;
        set
        {
            _value = Math.Clamp(value, _minimum, _maximum);
            Invalidate();
        }
    }

    [DefaultValue(false)]
    public bool Marquee
    {
        get => _marquee;
        set
        {
            if (_marquee == value)
            {
                return;
            }

            _marquee = value;
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
        _marqueeOffset += 0.02f;
        if (_marqueeOffset > 1f)
        {
            _marqueeOffset -= 1f;
        }

        if (_marquee && Visible)
        {
            Invalidate();
        }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;

        float radius = Math.Min(Height / 2f, 6f);
        var track = new RectangleF(0.5f, 0.5f, Width - 1f, Height - 1f);
        ThemeRenderer.FillRounded(g, track, radius, OpencodeTheme.BackgroundElement);

        if (_marquee)
        {
            float band = Math.Max(60f, Width * 0.3f);
            float x = -band + (Width + band) * _marqueeOffset;
            var clip = g.Clip;
            using var path = ThemeRenderer.RoundedRect(track, radius);
            g.SetClip(path);
            using var brush = new LinearGradientBrush(
                new RectangleF(x, 0, band, Height),
                OpencodeTheme.WithAlpha(OpencodeTheme.Primary, 60),
                OpencodeTheme.Primary,
                LinearGradientMode.Horizontal);
            g.FillRectangle(brush, x, 0f, band, Height);
            g.Clip = clip;
            return;
        }

        int range = Math.Max(1, _maximum - _minimum);
        float fraction = Math.Clamp((_value - _minimum) / (float)range, 0f, 1f);
        float fillWidth = (Width - 1f) * fraction;
        if (fillWidth <= 1f)
        {
            return;
        }

        var fill = new RectangleF(0.5f, 0.5f, Math.Max(1f, fillWidth), Height - 1f);
        Color color = fraction >= 1f ? OpencodeTheme.Success : OpencodeTheme.Primary;
        ThemeRenderer.FillRounded(g, fill, radius, color);
    }
}
