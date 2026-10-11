using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;

namespace DbMigration.App.Controls;

public sealed class ThemedComboBox : Control
{
    private readonly List<object> _items = new();
    private int _selectedIndex = -1;
    private bool _hover;
    private bool _open;

    public ThemedComboBox()
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
        Height = 34;
        TabStop = true;
        Cursor = Cursors.Hand;
    }

    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public IList<object> Items => _items;

    [DefaultValue(-1)]
    public int SelectedIndex
    {
        get => _selectedIndex;
        set
        {
            int clamped = value < -1 || value >= _items.Count ? -1 : value;
            if (_selectedIndex == clamped)
            {
                return;
            }

            _selectedIndex = clamped;
            Invalidate();
            SelectedIndexChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public object? SelectedItem
    {
        get => _selectedIndex >= 0 && _selectedIndex < _items.Count ? _items[_selectedIndex] : null;
        set => SelectedIndex = value is null ? -1 : _items.IndexOf(value);
    }

    [DefaultValue("")]
    public string Placeholder { get; set; } = string.Empty;

    public event EventHandler? SelectedIndexChanged;

    public void SetItems(IEnumerable<object> items)
    {
        _items.Clear();
        _items.AddRange(items);
        if (_selectedIndex >= _items.Count)
        {
            _selectedIndex = -1;
        }

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
        Invalidate();
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (e.Button == MouseButtons.Left)
        {
            Focus();
            ShowMenu();
        }
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        switch (e.KeyCode)
        {
            case Keys.Down:
                if (_items.Count > 0)
                {
                    SelectedIndex = Math.Min(_items.Count - 1, _selectedIndex + 1);
                }

                e.Handled = true;
                break;
            case Keys.Up:
                if (_items.Count > 0)
                {
                    SelectedIndex = Math.Max(0, _selectedIndex < 0 ? _items.Count - 1 : _selectedIndex - 1);
                }

                e.Handled = true;
                break;
            case Keys.Enter:
            case Keys.Space:
                ShowMenu();
                e.Handled = true;
                break;
        }
    }

    protected override bool IsInputKey(Keys keyData) =>
        keyData is Keys.Up or Keys.Down or Keys.Enter or Keys.Space || base.IsInputKey(keyData);

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

        var bounds = new RectangleF(0.5f, 0.5f, Width - 1f, Height - 1f);
        ThemeRenderer.FillRounded(g, bounds, ThemeRenderer.Radius, Enabled ? OpencodeTheme.BackgroundElement : OpencodeTheme.BackgroundPanel);
        ThemeRenderer.DrawRoundedBorder(g, bounds, ThemeRenderer.Radius, Focused || _open ? OpencodeTheme.Primary : _hover ? OpencodeTheme.BorderActive : OpencodeTheme.Border);

        var selected = SelectedItem?.ToString();
        var text = string.IsNullOrEmpty(selected) ? Placeholder : selected;
        var color = string.IsNullOrEmpty(selected) ? OpencodeTheme.TextMuted : ForeColor;
        var textRect = new Rectangle(12, 0, Math.Max(1, Width - 40), Height);
        TextRenderer.DrawText(g, text, Font, textRect, color, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix | TextFormatFlags.EndEllipsis);

        ThemeRenderer.DrawChevron(g, new RectangleF(Width - 28f, 0f, 16f, Height), OpencodeTheme.TextMuted, down: true);
    }

    private void ShowMenu()
    {
        if (!Enabled || _items.Count == 0)
        {
            return;
        }

        var menu = new ContextMenuStrip
        {
            Renderer = new DarkMenuRenderer(),
            ShowImageMargin = false,
            BackColor = OpencodeTheme.BackgroundPanel,
            ForeColor = OpencodeTheme.Text,
            Font = UiFonts.Body,
        };

        for (int i = 0; i < _items.Count; i++)
        {
            int index = i;
            string label = _items[i]?.ToString() ?? string.Empty;
            var item = new ToolStripMenuItem(i == _selectedIndex ? "✓ " + label : "   " + label)
            {
                ForeColor = OpencodeTheme.Text,
            };
            item.Click += (_, _) => SelectedIndex = index;
            menu.Items.Add(item);
        }

        menu.Closed += (_, _) =>
        {
            _open = false;
            Invalidate();
            BeginInvoke(new Action(menu.Dispose));
        };

        _open = true;
        Invalidate();
        menu.Show(this, new Point(0, Height + 2));
    }
}
