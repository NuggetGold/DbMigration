using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using DbMigration.App.Theme;

namespace DbMigration.App.Controls;

public enum CaptionButtonKind
{
    Minimize,
    MaximizeRestore,
    Close,
}

[ToolboxItem(false)]
public sealed class CaptionButton : Control
{
    private bool _hover;
    private bool _pressed;

    public CaptionButton(CaptionButtonKind kind)
    {
        Kind = kind;
        SetStyle(
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.UserPaint |
            ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.ResizeRedraw,
            true);
        Size = new Size(46, 32);
        TabStop = false;
        Cursor = Cursors.Hand;
        BackColor = OpencodeTheme.BackgroundPanel;
    }

    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public CaptionButtonKind Kind { get; }

    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool IsRestore { get; set; }

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
            Invalidate();
        }
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        _pressed = false;
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;

        Color background = Kind == CaptionButtonKind.Close && _hover
            ? (_pressed ? OpencodeTheme.Lerp(OpencodeTheme.Error, Color.Black, 0.2) : OpencodeTheme.Error)
            : _pressed ? OpencodeTheme.BackgroundElementActive
            : _hover ? OpencodeTheme.BackgroundElementHover
            : OpencodeTheme.BackgroundPanel;
        using (var brush = new SolidBrush(background))
        {
            g.FillRectangle(brush, ClientRectangle);
        }

        Color glyph = Kind == CaptionButtonKind.Close && _hover ? Color.White : OpencodeTheme.TextMuted;
        using var pen = new Pen(glyph, 1.3f) { StartCap = LineCap.Round, EndCap = LineCap.Round };
        float cx = Width / 2f;
        float cy = Height / 2f;

        switch (Kind)
        {
            case CaptionButtonKind.Minimize:
                g.DrawLine(pen, cx - 5f, cy + 4f, cx + 5f, cy + 4f);
                break;
            case CaptionButtonKind.Close:
                g.DrawLine(pen, cx - 4.5f, cy - 4.5f, cx + 4.5f, cy + 4.5f);
                g.DrawLine(pen, cx + 4.5f, cy - 4.5f, cx - 4.5f, cy + 4.5f);
                break;
            default:
                if (IsRestore)
                {
                    g.DrawRectangle(pen, cx - 5.5f, cy - 3.5f, 7f, 7f);
                    g.DrawLine(pen, cx - 3.5f, cy - 5.5f, cx + 5.5f, cy - 5.5f);
                    g.DrawLine(pen, cx + 5.5f, cy - 5.5f, cx + 5.5f, cy + 3.5f);
                }
                else
                {
                    g.DrawRectangle(pen, cx - 5f, cy - 5f, 10f, 10f);
                }

                break;
        }
    }
}

