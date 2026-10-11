using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using DbMigration.App.Theme;

namespace DbMigration.App.Controls;

public class ThemedForm : Form
{
    private const int WmNcHitTest = 0x0084;
    private const int HtClient = 1;
    private const int HtCaption = 2;
    private const int ResizeGrip = 6;

    private const int DwmwaUseImmersiveDarkMode = 20;
    private const int DwmwaWindowCornerPreference = 33;
    private const int DwmcpRound = 2;
    private const int DwmcpDoNotRound = 1;

    private readonly Panel _titleBar;
    private readonly GlowLabel _titleLabel;
    private readonly Label _subtitleLabel;
    private readonly CaptionButton _minimizeButton;
    private readonly CaptionButton _maximizeButton;
    private readonly CaptionButton _closeButton;
    private string _subtitle = string.Empty;

    public ThemedForm()
    {
        SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint, true);
        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = OpencodeTheme.Background;
        ForeColor = OpencodeTheme.Text;
        Font = UiFonts.Body;
        DoubleBuffered = true;
        MinimumSize = new Size(720, 480);
        Size = new Size(1180, 720);
        KeyPreview = true;

        _titleBar = new Panel
        {
            Dock = DockStyle.Top,
            Height = 46,
            BackColor = OpencodeTheme.BackgroundPanel,
        };
        _titleBar.Paint += PaintTitleBar;
        _titleBar.MouseDown += TitleBarMouseDown;

        _titleLabel = new GlowLabel
        {
            Text = "DbMigration",
            Font = AppFonts.Display(15f),
            GlowColor = OpencodeTheme.Primary,
            ShineEnabled = true,
            ShineSpeed = 110f,
            AutoSize = true,
            Location = new Point(16, 8),
        };
        _titleLabel.MouseDown += TitleBarMouseDown;
        _titleLabel.MouseDoubleClick += (_, _) => ToggleMaximize();

        _subtitleLabel = new Label
        {
            AutoSize = true,
            ForeColor = OpencodeTheme.TextMuted,
            BackColor = Color.Transparent,
            Font = UiFonts.BodySmall,
            Location = new Point(140, 16),
        };
        _subtitleLabel.MouseDown += TitleBarMouseDown;

        _closeButton = new CaptionButton(CaptionButtonKind.Close) { Dock = DockStyle.Right };
        _maximizeButton = new CaptionButton(CaptionButtonKind.MaximizeRestore) { Dock = DockStyle.Right };
        _minimizeButton = new CaptionButton(CaptionButtonKind.Minimize) { Dock = DockStyle.Right };

        _closeButton.Click += (_, _) => Close();
        _maximizeButton.Click += (_, _) => ToggleMaximize();
        _minimizeButton.Click += (_, _) => WindowState = FormWindowState.Minimized;

        _titleBar.Controls.Add(_subtitleLabel);
        _titleBar.Controls.Add(_titleLabel);
        _titleBar.Controls.Add(_minimizeButton);
        _titleBar.Controls.Add(_maximizeButton);
        _titleBar.Controls.Add(_closeButton);

        ContentPanel = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = OpencodeTheme.Background,
            Padding = new Padding(0),
        };

        Controls.Add(ContentPanel);
        Controls.Add(_titleBar);
    }

    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Panel ContentPanel { get; }

    [DefaultValue("")]
    public string Subtitle
    {
        get => _subtitle;
        set
        {
            _subtitle = value ?? string.Empty;
            _subtitleLabel.Text = _subtitle;
            _subtitleLabel.Left = _titleLabel.Right + 12;
            _subtitleLabel.Top = (46 - _subtitleLabel.Height) / 2;
        }
    }

    [DefaultValue("")]
    public string TitleText
    {
        get => _titleLabel.Text;
        set => _titleLabel.Text = value ?? string.Empty;
    }

    [DefaultValue(true)]
    public bool ShowTitleBar
    {
        get => _titleBar.Visible;
        set => _titleBar.Visible = value;
    }

    [DefaultValue(true)]
    public bool ShowMinimizeButton
    {
        get => _minimizeButton.Visible;
        set => _minimizeButton.Visible = value;
    }

    [DefaultValue(true)]
    public bool ShowMaximizeButton
    {
        get => _maximizeButton.Visible;
        set => _maximizeButton.Visible = value;
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        ApplyDwm();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        using var pen = new Pen(OpencodeTheme.Border);
        e.Graphics.DrawRectangle(pen, 0, 0, Width - 1, Height - 1);
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        if (_maximizeButton is not null)
        {
            _maximizeButton.IsRestore = WindowState == FormWindowState.Maximized;
        }

        UpdateCornerPreference();
    }

    private void PaintTitleBar(object? sender, PaintEventArgs e)
    {
        using var brush = new SolidBrush(OpencodeTheme.BackgroundPanel);
        e.Graphics.FillRectangle(brush, _titleBar.ClientRectangle);
        using var pen = new Pen(OpencodeTheme.BorderSubtle);
        e.Graphics.DrawLine(pen, 0, _titleBar.Height - 1, Width, _titleBar.Height - 1);
    }

    private void TitleBarMouseDown(object? sender, MouseEventArgs e)
    {
        if (e.Button != MouseButtons.Left)
        {
            return;
        }

        if (WindowState == FormWindowState.Maximized)
        {
            return;
        }

        ReleaseCapture();
        SendMessage(Handle, 0x00A1 , (IntPtr)HtCaption, IntPtr.Zero);
    }

    private void ToggleMaximize()
    {
        WindowState = WindowState == FormWindowState.Maximized
            ? FormWindowState.Normal
            : FormWindowState.Maximized;
        _maximizeButton.IsRestore = WindowState == FormWindowState.Maximized;
        UpdateCornerPreference();
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == WmNcHitTest)
        {
            base.WndProc(ref m);
            if ((int)m.Result == HtClient)
            {
                m.Result = (IntPtr)HitTest(unchecked((short)(long)m.LParam), unchecked((short)((long)m.LParam >> 16)));
            }

            return;
        }

        base.WndProc(ref m);
    }

    private int HitTest(int screenX, int screenY)
    {
        var client = PointToClient(new Point(screenX, screenY));

        if (WindowState == FormWindowState.Normal)
        {
            bool left = client.X <= ResizeGrip;
            bool right = client.X >= Width - ResizeGrip;
            bool top = client.Y <= ResizeGrip;
            bool bottom = client.Y >= Height - ResizeGrip;

            if (top && left)
            {
                return 13;
            }

            if (top && right)
            {
                return 14;
            }

            if (bottom && left)
            {
                return 16;
            }

            if (bottom && right)
            {
                return 17;
            }

            if (left)
            {
                return 10;
            }

            if (right)
            {
                return 11;
            }

            if (top)
            {
                return 12;
            }

            if (bottom)
            {
                return 15;
            }
        }

        if (client.Y >= 0 && client.Y < _titleBar.Height && !IsOverCaptionButton(screenX, screenY))
        {
            return HtCaption;
        }

        return HtClient;
    }

    private bool IsOverCaptionButton(int screenX, int screenY)
    {
        foreach (var button in new[] { _minimizeButton, _maximizeButton, _closeButton })
        {
            if (button.RectangleToScreen(button.ClientRectangle).Contains(screenX, screenY))
            {
                return true;
            }
        }

        return false;
    }

    private void ApplyDwm()
    {
        try
        {
            int dark = 1;
            DwmSetWindowAttribute(Handle, DwmwaUseImmersiveDarkMode, ref dark, sizeof(int));
        }
        catch (DllNotFoundException)
        {
        }

        UpdateCornerPreference();
    }

    private void UpdateCornerPreference()
    {
        if (!IsHandleCreated)
        {
            return;
        }

        try
        {
            int preference = WindowState == FormWindowState.Maximized ? DwmcpDoNotRound : DwmcpRound;
            DwmSetWindowAttribute(Handle, DwmwaWindowCornerPreference, ref preference, sizeof(int));
        }
        catch (DllNotFoundException)
        {
        }
    }

    [DllImport("dwmapi.dll", SetLastError = true)]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);

    [DllImport("user32.dll")]
    private static extern bool ReleaseCapture();

    [DllImport("user32.dll")]
    private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);
}

