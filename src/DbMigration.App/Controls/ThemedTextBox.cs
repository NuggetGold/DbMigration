using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Drawing.Drawing2D;
using DbMigration.App.Theme;

namespace DbMigration.App.Controls;

public sealed class ThemedTextBox : Control
{
    private readonly TextBox _editor;
    private string _placeholder = string.Empty;
    private bool _focused;

    public ThemedTextBox()
    {
        SetStyle(
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.UserPaint |
            ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.ResizeRedraw |
            ControlStyles.SupportsTransparentBackColor,
            true);

        _editor = new TextBox
        {
            BorderStyle = BorderStyle.None,
            BackColor = OpencodeTheme.BackgroundElement,
            ForeColor = OpencodeTheme.Text,
            Font = UiFonts.Body,
            TabIndex = 0,
        };
        _editor.GotFocus += OnEditorGotFocus;
        _editor.LostFocus += OnEditorLostFocus;
        _editor.TextChanged += OnEditorTextChanged;
        _editor.KeyDown += OnEditorKeyDown;
        Controls.Add(_editor);

        BackColor = Color.Transparent;
        Font = UiFonts.Body;
        Height = 36;
        TabStop = false;
    }

    public TextBox Editor => _editor;

    [DefaultValue("")]
    public string Placeholder
    {
        get => _placeholder;
        set
        {
            _placeholder = value ?? string.Empty;
            Invalidate();
        }
    }

    [AllowNull]
    public override string Text
    {
        get => _editor.Text;
        set
        {
            string text = value ?? string.Empty;
            if (_editor.Text != text)
            {
                _editor.Text = text;
            }
        }
    }

    [DefaultValue(false)]
    public bool Multiline
    {
        get => _editor.Multiline;
        set
        {
            if (_editor.Multiline == value)
            {
                return;
            }

            _editor.Multiline = value;
            _editor.AcceptsReturn = value;
            _editor.ScrollBars = value ? ScrollBars.Both : ScrollBars.None;
            _editor.WordWrap = false;
            LayoutEditor();
            Invalidate();
        }
    }

    [DefaultValue(false)]
    public bool UseMonoFont
    {
        get => ReferenceEquals(_editor.Font, UiFonts.Mono);
        set
        {
            _editor.Font = value ? UiFonts.Mono : UiFonts.Body;
            Invalidate();
        }
    }

    [DefaultValue(false)]
    public bool ReadOnly
    {
        get => _editor.ReadOnly;
        set => _editor.ReadOnly = value;
    }

    [DefaultValue(CharacterCasing.Normal)]
    public CharacterCasing CharacterCasing
    {
        get => _editor.CharacterCasing;
        set => _editor.CharacterCasing = value;
    }

    public event EventHandler<KeyEventArgs>? EditorKeyDown;

    public void SelectAll() => _editor.SelectAll();

    public new void Focus() => _editor.Focus();

    protected override void OnFontChanged(EventArgs e)
    {
        base.OnFontChanged(e);
        if (_editor is not null && _editor.Multiline)
        {
            LayoutEditor();
        }
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        LayoutEditor();
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
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;

        var bounds = new RectangleF(0.5f, 0.5f, Width - 1f, Height - 1f);
        Color fill = Enabled ? OpencodeTheme.BackgroundElement : OpencodeTheme.BackgroundPanel;
        ThemeRenderer.FillRounded(g, bounds, ThemeRenderer.Radius, fill);
        ThemeRenderer.DrawRoundedBorder(
            g,
            bounds,
            ThemeRenderer.Radius,
            _focused ? OpencodeTheme.Primary : OpencodeTheme.Border,
            _focused ? 1.6f : 1f);

        if (Enabled && !_focused && _editor.TextLength == 0 && _placeholder.Length > 0)
        {
            int padLeft = 12;
            var rect = new Rectangle(padLeft, 0, Math.Max(1, Width - padLeft * 2), Height);
            TextRenderer.DrawText(
                g,
                _placeholder,
                Font,
                rect,
                OpencodeTheme.TextMuted,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
        }
    }

    private void OnEditorGotFocus(object? sender, EventArgs e)
    {
        _focused = true;
        Invalidate();
    }

    private void OnEditorLostFocus(object? sender, EventArgs e)
    {
        _focused = false;
        Invalidate();
    }

    private void OnEditorTextChanged(object? sender, EventArgs e)
    {
        base.OnTextChanged(EventArgs.Empty);
        Invalidate();
    }

    private void OnEditorKeyDown(object? sender, KeyEventArgs e)
    {
        EditorKeyDown?.Invoke(this, e);
        if (!e.Handled)
        {
            base.OnKeyDown(e);
        }
    }

    private void LayoutEditor()
    {
        if (_editor is null)
        {
            return;
        }

        const int padLeft = 12;
        const int padRight = 12;
        const int padTop = 8;
        const int padBottom = 8;

        if (_editor.Multiline)
        {
            _editor.Bounds = new Rectangle(
                padLeft,
                padTop,
                Math.Max(1, Width - padLeft - padRight),
                Math.Max(1, Height - padTop - padBottom));
        }
        else
        {
            _editor.Width = Math.Max(1, Width - padLeft - padRight);
            _editor.Left = padLeft;
            _editor.Top = Math.Max(padTop, (Height - _editor.Height) / 2);
        }
    }
}

