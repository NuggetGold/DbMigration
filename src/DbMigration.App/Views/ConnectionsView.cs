using System.Drawing;
using System.Windows.Forms;

namespace DbMigration.App.Views;

public sealed class ConnectionsView : ViewBase
{
    private readonly ThemedListBox _list = new();
    private readonly GlowLabel _detailName = new() { Font = UiFonts.DisplayLarge, GlowColor = OpencodeTheme.Primary, AutoSize = true };
    private readonly Label _detailEngine = ViewUi.Label(string.Empty, OpencodeTheme.Text);
    private readonly Label _detailTarget = ViewUi.Label(string.Empty, OpencodeTheme.Text);
    private readonly Label _detailRole = ViewUi.Label(string.Empty, OpencodeTheme.Text);
    private readonly Label _detailUsed = ViewUi.Label(string.Empty, OpencodeTheme.Text);
    private readonly GlowLabel _status = new() { Font = UiFonts.Body, AutoSize = true, Animate = false, GlowRadius = 5, MaxIntensity = 0.5f };
    private readonly ThemedButton _useSource = ViewUi.Button("Сделать источником", ButtonVariant.Primary);
    private readonly ThemedButton _useTarget = ViewUi.Button("Сделать целью");

    public ConnectionsView(AppState state)
        : base(state)
    {
        _list.Dock = DockStyle.Fill;
        _list.TextResolver = item => item is ConnectionProfile p ? $"{p.Name}   ·   {ViewUi.EngineName(p.Engine)}" : string.Empty;
        _list.ColorResolver = item => item is ConnectionProfile p
            ? ReferenceEquals(p, State.SourceProfile) ? OpencodeTheme.Primary
                : ReferenceEquals(p, State.TargetProfile) ? OpencodeTheme.Success
                : OpencodeTheme.Text
            : null;
        _list.SelectedIndexChanged += (_, _) => ShowDetails();
        _list.DoubleClick += (_, _) => EditSelected();

        var listHost = new Card { Padding = new Padding(10, 12, 10, 10) };
        listHost.Controls.Add(_list);

        var body = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            BackColor = OpencodeTheme.Background,
        };
        body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 42f));
        body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 58f));
        body.Controls.Add(listHost, 0, 0);
        body.Controls.Add(BuildDetails(), 1, 0);

        var add = ViewUi.Button("Добавить", ButtonVariant.Primary);
        var edit = ViewUi.Button("Изменить");
        var remove = ViewUi.Button("Удалить", ButtonVariant.Danger);
        var test = ViewUi.Button("Проверить связь");
        add.Click += (_, _) => AddProfile();
        edit.Click += (_, _) => EditSelected();
        remove.Click += async (_, _) => await DeleteSelectedAsync();
        test.Click += async (_, _) => await TestSelectedAsync();

        _useSource.Click += (_, _) => Assign(true);
        _useTarget.Click += (_, _) => Assign(false);

        Controls.Add(body);
        Controls.Add(ViewUi.Toolbar(add, edit, remove, test));
        Controls.Add(ViewUi.Header("Подключения", "Профили баз данных для миграции, просмотра и резервного копирования."));

        State.ProfilesChanged += (_, _) => Reload();
        State.SelectionChanged += (_, _) =>
        {
            _list.Invalidate();
            ShowDetails();
        };

        Reload();
    }

    public override string ViewTitle => "Подключения";

    public override Task ActivateAsync()
    {
        Reload();
        return Task.CompletedTask;
    }

    private ConnectionProfile? Selected => _list.SelectedItem as ConnectionProfile;

    private Control BuildDetails()
    {
        var card = ViewUi.Card("Выбранное подключение");
        var flow = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoScroll = true,
            BackColor = Color.Transparent,
        };

        flow.Controls.Add(_detailName);
        flow.Controls.Add(ViewUi.Label("СУБД", OpencodeTheme.TextMuted, UiFonts.BodySmall));
        flow.Controls.Add(_detailEngine);
        flow.Controls.Add(ViewUi.Label("Адрес", OpencodeTheme.TextMuted, UiFonts.BodySmall));
        flow.Controls.Add(_detailTarget);
        flow.Controls.Add(ViewUi.Label("Роль", OpencodeTheme.TextMuted, UiFonts.BodySmall));
        flow.Controls.Add(_detailRole);
        flow.Controls.Add(ViewUi.Label("Последнее использование", OpencodeTheme.TextMuted, UiFonts.BodySmall));
        flow.Controls.Add(_detailUsed);
        flow.Controls.Add(_status);
        var actions = new FlowLayoutPanel
        {
            AutoSize = true,
            WrapContents = false,
            BackColor = Color.Transparent,
            Margin = new Padding(0, 8, 0, 0),
        };
        actions.Controls.Add(_useSource);
        actions.Controls.Add(_useTarget);
        flow.Controls.Add(actions);

        card.Controls.Add(flow);
        return card;
    }

    private void Reload()
    {
        var selected = Selected;
        _list.BeginUpdate();
        _list.Items.Clear();
        foreach (var profile in State.Profiles)
        {
            _list.Items.Add(profile);
        }

        _list.EndUpdate();

        if (selected is not null && _list.Items.Contains(selected))
        {
            _list.SelectedItem = selected;
        }
        else if (_list.Items.Count > 0)
        {
            _list.SelectedIndex = 0;
        }

        ShowDetails();
    }

    private void ShowDetails()
    {
        var profile = Selected;
        if (profile is null)
        {
            _detailName.Text = "Нет выбора";
            _detailEngine.Text = "—";
            _detailTarget.Text = "—";
            _detailRole.Text = "—";
            _detailUsed.Text = "—";
            _useSource.Enabled = false;
            _useTarget.Enabled = false;
            return;
        }

        _detailName.Text = profile.Name;
        _detailEngine.Text = ViewUi.EngineName(profile.Engine);
        _detailTarget.Text = profile.DisplayTarget;
        _detailRole.Text = Role(profile);
        _detailUsed.Text = profile.LastUsedUtc.ToLocalTime().ToString("g");
        _useSource.Enabled = true;
        _useTarget.Enabled = true;
    }

    private string Role(ConnectionProfile profile)
    {
        bool source = ReferenceEquals(profile, State.SourceProfile);
        bool target = ReferenceEquals(profile, State.TargetProfile);
        return (source, target) switch
        {
            (true, true) => "Источник и цель",
            (true, false) => "Источник",
            (false, true) => "Цель",
            _ => "—",
        };
    }

    private void Assign(bool asSource)
    {
        if (Selected is not { } profile)
        {
            return;
        }

        if (asSource)
        {
            State.SetSource(profile);
            State.Log(LogLevel.Info, $"Источник: {profile.Name}.");
        }
        else
        {
            State.SetTarget(profile);
            State.Log(LogLevel.Info, $"Цель: {profile.Name}.");
        }
    }

    private async void AddProfile()
    {
        using var dialog = new ConnectionEditorDialog();
        if (dialog.ShowDialog(this) == DialogResult.OK && dialog.Result is { } profile)
        {
            try
            {
                await State.UpsertProfileAsync(profile);
                Reload();
            }
            catch (Exception ex)
            {
                State.Log(LogLevel.Error, $"Не удалось сохранить подключение: {ex.Message}");
                ThemedMessageBox.Error(this, "Подключения", ex.Message);
            }
        }
    }

    private void EditSelected()
    {
        if (Selected is not { } profile)
        {
            return;
        }

        using var dialog = new ConnectionEditorDialog(profile);
        if (dialog.ShowDialog(this) == DialogResult.OK && dialog.Result is { } edited)
        {
            _ = SaveEditedAsync(edited);
        }
    }

    private async Task SaveEditedAsync(ConnectionProfile profile)
    {
        try
        {
            await State.UpsertProfileAsync(profile);
            Reload();
        }
        catch (Exception ex)
        {
            State.Log(LogLevel.Error, $"Не удалось сохранить подключение: {ex.Message}");
            ThemedMessageBox.Error(this, "Подключения", ex.Message);
        }
    }

    private async Task DeleteSelectedAsync()
    {
        if (Selected is not { } profile)
        {
            return;
        }

        if (!ThemedMessageBox.Confirm(this, "Удаление", $"Удалить подключение «{profile.Name}»?", destructive: true))
        {
            return;
        }

        try
        {
            await State.RemoveProfileAsync(profile);
            Reload();
        }
        catch (Exception ex)
        {
            State.Log(LogLevel.Error, $"Не удалось удалить подключение: {ex.Message}");
            ThemedMessageBox.Error(this, "Подключения", ex.Message);
        }
    }

    private async Task TestSelectedAsync()
    {
        if (Selected is not { } profile)
        {
            return;
        }

        _status.Text = "Проверка подключения…";
        _status.GlowColor = OpencodeTheme.Info;

        try
        {
            var provider = State.Services.Providers.Get(profile.Engine);
            string description = await provider.TestConnectionAsync(profile);
            _status.Text = $"Связь есть · {description}";
            _status.GlowColor = OpencodeTheme.Success;
            State.Log(LogLevel.Success, $"Подключение «{profile.Name}» проверено: {description}.");
        }
        catch (Exception ex)
        {
            _status.Text = $"Не удалось подключиться · {ex.Message}";
            _status.GlowColor = OpencodeTheme.Error;
            State.Log(LogLevel.Error, $"Ошибка подключения «{profile.Name}»: {ex.Message}");
        }
    }
}
