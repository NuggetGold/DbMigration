using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;

namespace DbMigration.App.Controls;

public sealed class ToggleSwitch : Control
{
    private bool _checked;
    private float _position;
    private bool _animating;

    public ToggleSwitch()
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
        Size = new Size(48, 26);
        Cursor = Cursors.Hand;
        TabStop = true;
    }

    [DefaultValue(false)]
    public bool Checked
    {
        get => _checked;
        set
        {
            if (_checked == value)
            {
                return;
            }

            _checked = value;
            Invalidate();
            CheckedChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public event EventHandler? CheckedChanged;

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        _position = _checked ? 1f : 0f;
        AnimationClock.Instance.Tick += OnTick;
    }

    protected override void OnHandleDestroyed(EventArgs e)
    {
        AnimationClock.Instance.Tick -= OnTick;
        base.OnHandleDestroyed(e);
    }

    private void OnTick(object? sender, EventArgs e)
    {
        float target = _checked ? 1f : 0f;
        if (Math.Abs(target - _position) < 0.02f)
        {
            if (_animating)
            {
                _position = target;
                _animating = false;
                Invalidate();
            }

            return;
        }

        _position += (target - _position) * 0.24f;
        _animating = true;
        Invalidate();
    }

    protected override void OnClick(EventArgs e)
    {
        base.OnClick(e);
        Focus();
        Checked = !Checked;
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.KeyCode is Keys.Space or Keys.Enter)
        {
            Checked = !Checked;
            e.Handled = true;
        }
    }

    protected override bool IsInputKey(Keys keyData) => keyData is Keys.Space || base.IsInputKey(keyData);

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

        float height = Height - 2f;
        var track = new RectangleF(1f, 1f, Width - 2f, height);
        float radius = height / 2f;

        Color trackColor = OpencodeTheme.Lerp(OpencodeTheme.BackgroundElement, OpencodeTheme.Primary, _position);
        ThemeRenderer.FillRounded(g, track, radius, trackColor);
        ThemeRenderer.DrawRoundedBorder(g, track, radius, OpencodeTheme.Lerp(OpencodeTheme.Border, OpencodeTheme.PrimaryBright, _position));

        float knobDiameter = height - 6f;
        float travel = Width - 2f - knobDiameter - 6f;
        float knobX = 4f + travel * _position;
        var knob = new RectangleF(knobX, 4f, knobDiameter, knobDiameter);
        using var knobBrush = new SolidBrush(OpencodeTheme.Text);
        g.FillEllipse(knobBrush, knob);
    }
}
