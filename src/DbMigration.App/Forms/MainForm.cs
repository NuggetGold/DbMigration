using System.Drawing;
using System.Windows.Forms;

namespace DbMigration.App.Forms;

public sealed class MainForm : ThemedForm
{
    private sealed record NavEntry(string Title, ThemedButton Button, Func<ViewBase> Create);

    private readonly AppState _state;
    private readonly Panel _nav;
    private readonly Panel _host;
    private readonly StatusBarControl _statusBar;
    private readonly List<NavEntry> _entries = new();
    private readonly Dictionary<string, ViewBase> _views = new();

    public MainForm(AppState state)
    {
        _state = state;
        TitleText = "DbMigration";
        Subtitle = "Миграция и конвертация баз данных";
        Text = "DbMigration";

        _host = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = OpencodeTheme.Background,
        };

        _nav = BuildNav();

        _statusBar = new StatusBarControl
        {
            Status = "Готово",
        };

        ContentPanel.Controls.Add(_host);
        ContentPanel.Controls.Add(_nav);
        ContentPanel.Controls.Add(_statusBar);

        state.SelectionChanged += (_, _) => UpdateStatus();
        state.ProfilesChanged += (_, _) => UpdateStatus();

        SwitchTo("Подключения");
        UpdateStatus();
    }

    private Panel BuildNav()
    {
        var nav = new Panel
        {
            Dock = DockStyle.Left,
            Width = 220,
            BackColor = OpencodeTheme.BackgroundPanel,
        };

        var navList = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoScroll = true,
            BackColor = Color.Transparent,
            Padding = new Padding(10, 18, 10, 8),
        };

        Register("Подключения", "⬡", () => new ConnectionsView(_state));
        Register("Схема", "▤", () => new SchemaView(_state));
        Register("Данные", "▦", () => new DataView(_state));
        Register("Миграция", "⇄", () => new MigrationView(_state));
        Register("SQL-консоль", "▣", () => new SqlConsoleView(_state));
        Register("Резервное копирование", "☁", () => new BackupView(_state));
        Register("Журнал", "≡", () => new LogsView(_state));
        Register("Настройки", "⚙", () => new SettingsView(_state));
        Register("О программе", "ℹ", () => new AboutView(_state));

        foreach (var entry in _entries)
        {
            entry.Button.Width = 200;
            entry.Button.Height = 36;
            entry.Button.Margin = new Padding(0, 0, 0, 4);
            entry.Button.TextAlign = ContentAlignment.MiddleLeft;
            entry.Button.Tag = entry.Title;
            entry.Button.Click += OnNavClick;
            navList.Controls.Add(entry.Button);
        }

        nav.Controls.Add(navList);
        nav.Controls.Add(BuildFooter());

        var grip = new Panel
        {
            Dock = DockStyle.Right,
            Width = 1,
            BackColor = OpencodeTheme.BorderSubtle,
        };
        nav.Controls.Add(grip);

        return nav;
    }

    private Panel BuildFooter()
    {
        var footer = new Panel
        {
            Dock = DockStyle.Bottom,
            Height = 100,
            BackColor = OpencodeTheme.BackgroundElement,
        };
        footer.Paint += (_, e) =>
        {
            using var pen = new Pen(OpencodeTheme.BorderSubtle);
            e.Graphics.DrawLine(pen, 0, 0, footer.Width, 0);
        };

        var github = new ThemedLink
        {
            Text = "github.com/NuggetGold",
            Url = "https://github.com/NuggetGold",
            Animate = true,
            PulseFrom = OpencodeTheme.PurpleDark,
            PulseTo = OpencodeTheme.PurpleLight,
            AutoSize = true,
            Location = new Point(14, 10),
        };

        var author = new Label
        {
            Text = "Автор: @NuggetGold",
            ForeColor = OpencodeTheme.SkyBlue,
            Font = UiFonts.BodySmall,
            BackColor = Color.Transparent,
            AutoSize = true,
            Location = new Point(14, 40),
        };

        var hint = new Label
        {
            Text = "Данные хранятся локально",
            ForeColor = OpencodeTheme.TextMuted,
            Font = UiFonts.Caption,
            BackColor = Color.Transparent,
            AutoSize = true,
            Location = new Point(14, 62),
        };

        var shortcut = new Label
        {
            Text = "F8 — журнал · F1 — о программе",
            ForeColor = OpencodeTheme.TextMuted,
            Font = UiFonts.Caption,
            BackColor = Color.Transparent,
            AutoSize = true,
            Location = new Point(14, 80),
        };

        footer.Controls.Add(github);
        footer.Controls.Add(author);
        footer.Controls.Add(hint);
        footer.Controls.Add(shortcut);
        return footer;
    }

    private void Register(string title, string glyph, Func<ViewBase> create)
    {
        var button = new ThemedButton
        {
            Text = title,
            Glyph = glyph,
            Variant = ButtonVariant.Ghost,
        };
        _entries.Add(new NavEntry(title, button, create));
    }

    private void OnNavClick(object? sender, EventArgs e)
    {
        if (sender is ThemedButton button && button.Tag is string title)
        {
            SwitchTo(title);
        }
    }

    private void SwitchTo(string title)
    {
        if (!_views.TryGetValue(title, out var view))
        {
            var entry = _entries.FirstOrDefault(e => e.Title == title);
            if (entry is null)
            {
                return;
            }

            view = entry.Create();
            _views[title] = view;
        }

        _host.Controls.Clear();
        _host.Controls.Add(view);

        foreach (var entry in _entries)
        {
            entry.Button.Active = entry.Title == title;
        }

        ActivateView(view);
    }

    private async void ActivateView(ViewBase view)
    {
        try
        {
            await view.ActivateAsync();
        }
        catch (Exception ex)
        {
            _state.Log(LogLevel.Error, $"Не удалось открыть экран: {ex.Message}");
        }
    }

    private async void SaveProfilesSafely()
    {
        try
        {
            await _state.SaveProfilesAsync();
        }
        catch (Exception ex)
        {
            _state.Log(LogLevel.Error, $"Не удалось сохранить подключения: {ex.Message}");
        }
    }

    private void UpdateStatus()
    {
        string source = _state.SourceProfile?.Name ?? string.Empty;
        string target = _state.TargetProfile?.Name ?? string.Empty;
        string selection = source.Length > 0 && target.Length > 0
            ? $"{source}  →  {target}"
            : source.Length > 0 ? $"Источник: {source}"
            : target.Length > 0 ? $"Цель: {target}"
            : "Источник и цель не выбраны";
        _statusBar.Status = "Готово";
        _statusBar.LeftInfo = $"Профилей: {_state.Profiles.Count} · Ctrl+K — командная палитра";
        _statusBar.RightInfo = selection;
    }

    private void ShowPalette()
    {
        var items = new List<PaletteItem>();
        foreach (var entry in _entries)
        {
            string title = entry.Title;
            items.Add(new PaletteItem(title, "Экраны", () => SwitchTo(title)));
        }

        items.Add(new PaletteItem("Сохранить подключения", "Действия", SaveProfilesSafely));
        items.Add(new PaletteItem("Открыть журнал", "Действия", () => SwitchTo("Журнал")));

        using var palette = new CommandPalette(items);
        palette.ShowDialog(this);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);

        if (e.Control && e.KeyCode == Keys.K)
        {
            ShowPalette();
            e.Handled = true;
            return;
        }

        switch (e.KeyCode)
        {
            case Keys.F1:
                SwitchTo("О программе");
                e.Handled = true;
                break;
            case Keys.F8:
                SwitchTo("Журнал");
                e.Handled = true;
                break;
        }
    }
}