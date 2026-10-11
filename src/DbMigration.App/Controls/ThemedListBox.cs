using System.ComponentModel;
using System.Drawing;

namespace DbMigration.App.Controls;

public sealed class ThemedListBox : ListBox
{
    private Func<object, Color?>? _colorResolver;
    private Func<object, string>? _textResolver;

    public ThemedListBox()
    {
        DrawMode = DrawMode.OwnerDrawFixed;
        BorderStyle = BorderStyle.None;
        BackColor = OpencodeTheme.BackgroundPanel;
        ForeColor = OpencodeTheme.Text;
        Font = UiFonts.Body;
        ItemHeight = 28;
        IntegralHeight = false;
    }

    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Func<object, string>? TextResolver
    {
        get => _textResolver;
        set
        {
            _textResolver = value;
            Invalidate();
        }
    }

    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Func<object, Color?>? ColorResolver
    {
        get => _colorResolver;
        set
        {
            _colorResolver = value;
            Invalidate();
        }
    }

    protected override void OnDrawItem(DrawItemEventArgs e)
    {
        if (e.Index < 0 || e.Index >= Items.Count)
        {
            return;
        }

        var g = e.Graphics;
        bool selected = (e.State & DrawItemState.Selected) != 0;
        using (var brush = new SolidBrush(selected ? OpencodeTheme.WithAlpha(OpencodeTheme.Accent, 130) : OpencodeTheme.BackgroundPanel))
        {
            g.FillRectangle(brush, e.Bounds);
        }

        if (selected)
        {
            using var accent = new SolidBrush(OpencodeTheme.Primary);
            g.FillRectangle(accent, e.Bounds.Left, e.Bounds.Top + 4, 3, e.Bounds.Height - 8);
        }

        object item = Items[e.Index];
        Color color = _colorResolver?.Invoke(item) ?? OpencodeTheme.Text;
        string text = _textResolver?.Invoke(item) ?? GetItemText(item) ?? string.Empty;
        var rect = new Rectangle(e.Bounds.Left + 12, e.Bounds.Top, Math.Max(1, e.Bounds.Width - 16), e.Bounds.Height);
        TextRenderer.DrawText(
            g,
            text,
            Font,
            rect,
            color,
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix | TextFormatFlags.EndEllipsis);
    }
}
