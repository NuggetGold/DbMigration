using System.Drawing;
using System.Globalization;
using System.Windows.Forms;

namespace DbMigration.App.Views;

public sealed class BackupView : ViewBase
{
    private static readonly ExportFormat[] Formats = { ExportFormat.SqlScript, ExportFormat.Csv, ExportFormat.Json };

    private readonly ThemedComboBox _profileCombo = new() { Width = 230, Placeholder = "Подключение" };
    private readonly ThemedComboBox _formatCombo = new() { Width = 200 };
    private readonly ToggleSwitch _includeData = new();
    private readonly ThemedListBox _tableList = new() { Dock = DockStyle.Fill, SelectionMode = SelectionMode.MultiExtended };
    private readonly GlowLabel _status = new() { Font = UiFonts.Body, AutoSize = true, Animate = false, GlowRadius = 5, MaxIntensity = 0.5f };
    private readonly ThemedButton _reload = ViewUi.Button("Обновить таблицы", ButtonVariant.Default, 180);
    private readonly ThemedButton _export = ViewUi.Button("Экспорт в папку…", ButtonVariant.Primary, 190);
    private readonly ThemedButton _cloud = ViewUi.Button("Загрузить в облако", ButtonVariant.Default, 200);
    private readonly ThemedButton _selectAll = ViewUi.Button("Выбрать все", ButtonVariant.Ghost, 140);
    private readonly ThemedButton _clearSel = ViewUi.Button("Снять", ButtonVariant.Ghost, 110);
    private List<ConnectionProfile> _profiles = new();
    private IReadOnlyList<TableSchema> _schema = Array.Empty<TableSchema>();

    public BackupView(AppState state)
        : base(state)
    {
        _formatCombo.SetItems(Formats.Select(f => (object)State.Services.Exporter.GetDisplayName(f)));
        _formatCombo.SelectedIndex = 0;
        _includeData.Checked = true;
        _tableList.TextResolver = item => item?.ToString() ?? string.Empty;

        _profileCombo.SelectedIndexChanged += async (_, _) => await LoadTablesAsync();
        _reload.Click += async (_, _) => await LoadTablesAsync();
        _selectAll.Click += (_, _) => SetAllSelected(true);
        _clearSel.Click += (_, _) => SetAllSelected(false);
        _export.Click += async (_, _) => await ExportToFolderAsync();
        _cloud.Click += (_, _) => UploadToCloud();

        var left = ViewUi.Card("Параметры");
        var leftFlow = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoScroll = true,
            BackColor = Color.Transparent,
        };
        leftFlow.Controls.Add(ViewUi.Label("Подключение", OpencodeTheme.TextMuted, UiFonts.BodySmall));
        leftFlow.Controls.Add(_profileCombo);
        leftFlow.Controls.Add(ViewUi.Label("Формат файлов", OpencodeTheme.TextMuted, UiFonts.BodySmall));
        leftFlow.Controls.Add(_formatCombo);
        leftFlow.Controls.Add(MakeToggleRow("Включить данные", _includeData));
        leftFlow.Controls.Add(ViewUi.Label("Действия", OpencodeTheme.TextMuted, UiFonts.BodySmall));
        leftFlow.Controls.Add(_export);
        leftFlow.Controls.Add(_cloud);
        leftFlow.Controls.Add(_status);
        left.Controls.Add(leftFlow);

        var right = ViewUi.Card("Таблицы");
        var rightPanel = new Panel { Dock = DockStyle.Fill, BackColor = Color.Transparent };
        var selToolbar = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            AutoSize = true,
            WrapContents = false,
            BackColor = Color.Transparent,
        };
        selToolbar.Controls.Add(_selectAll);
        selToolbar.Controls.Add(_clearSel);
        rightPanel.Controls.Add(_tableList);
        rightPanel.Controls.Add(selToolbar);
        right.Controls.Add(rightPanel);

        var body = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            BackColor = OpencodeTheme.Background,
        };
        body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 46f));
        body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 54f));
        body.Controls.Add(left, 0, 0);
        body.Controls.Add(right, 1, 0);

        Controls.Add(body);
        Controls.Add(ViewUi.Toolbar(_reload));
        Controls.Add(ViewUi.Header("Резервное копирование", "Экспорт таблиц в файлы и загрузка резервных копий в облачное хранилище."));

        State.ProfilesChanged += (_, _) => ReloadProfiles();
        ReloadProfiles();
    }

    public override string ViewTitle => "Резервное копирование";

    public override Task ActivateAsync()
    {
        ReloadProfiles();
        return Task.CompletedTask;
    }

    private static Control MakeToggleRow(string text, ToggleSwitch toggle)
    {
        var panel = new Panel { Width = 320, Height = 34, BackColor = Color.Transparent };
        var label = new Label
        {
            Text = text,
            ForeColor = OpencodeTheme.Text,
            Font = UiFonts.Body,
            AutoSize = true,
            Location = new Point(0, 6),
        };
        toggle.SetBounds(236, 4, 48, 26);
        panel.Controls.Add(toggle);
        panel.Controls.Add(label);
        return panel;
    }

    private ConnectionProfile? SelectedProfile =>
        _profileCombo.SelectedIndex >= 0 && _profileCombo.SelectedIndex < _profiles.Count
            ? _profiles[_profileCombo.SelectedIndex]
            : null;

    private void ReloadProfiles()
    {
        var previous = SelectedProfile;
        _profiles = State.Profiles.ToList();
        _profileCombo.SetItems(_profiles.Select(p => (object)p.Name));
        int index = previous is null ? 0 : Math.Max(0, _profiles.FindIndex(p => p.Id == previous.Id));
        _profileCombo.SelectedIndex = _profiles.Count > 0 ? index : -1;
    }

    private async Task LoadTablesAsync()
    {
        _tableList.Items.Clear();
        _schema = Array.Empty<TableSchema>();
        if (SelectedProfile is not { } profile)
        {
            return;
        }

        try
        {
            var provider = State.Services.Providers.Get(profile.Engine);
            _schema = await provider.GetSchemaAsync(profile);
            foreach (var table in _schema)
            {
                _tableList.Items.Add(table.Name);
            }

            if (_tableList.Items.Count > 0)
            {
                _tableList.SelectedIndex = 0;
            }

            SetStatus($"Таблиц: {_schema.Count}.", OpencodeTheme.Info);
        }
        catch (Exception ex)
        {
            SetStatus("Ошибка: " + ex.Message, OpencodeTheme.Error);
        }
    }

    private void SetAllSelected(bool selected)
    {
        for (int i = 0; i < _tableList.Items.Count; i++)
        {
            _tableList.SetSelected(i, selected);
        }
    }

    private IReadOnlyList<string> SelectedTables() =>
        _tableList.SelectedItems.Cast<object>().Select(o => o.ToString() ?? string.Empty).Where(s => s.Length > 0).ToList();

    private async Task ExportToFolderAsync()
    {
        if (SelectedProfile is not { } profile)
        {
            ThemedMessageBox.Warning(this, "Резервное копирование", "Выберите подключение.");
            return;
        }

        var chosen = SelectedTables();
        if (chosen.Count == 0)
        {
            ThemedMessageBox.Warning(this, "Резервное копирование", "Выберите хотя бы одну таблицу.");
            return;
        }

        using var dialog = new FolderBrowserDialog
        {
            Description = "Куда сохранить файлы резервных копий",
            UseDescriptionForTitle = true,
        };

        string configured = State.Settings.BackupFolder;
        if (configured.Length > 0 && Directory.Exists(configured))
        {
            dialog.SelectedPath = configured;
        }

        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        State.Settings.BackupFolder = dialog.SelectedPath;
        State.Services.Settings.Save();

        ExportFormat format = Formats[Math.Max(0, _formatCombo.SelectedIndex)];
        string extension = State.Services.Exporter.GetExtension(format);
        int saved = 0;

        try
        {
            foreach (var name in chosen)
            {
                var schema = _schema.FirstOrDefault(t => string.Equals(t.Name, name, StringComparison.OrdinalIgnoreCase));
                if (schema is null)
                {
                    continue;
                }

                string path = Path.Combine(dialog.SelectedPath, $"{Sanitize(name)}.{extension}");
                await using var stream = File.Create(path);
                await State.Services.Exporter.ExportTableAsync(profile, schema, format, stream, _includeData.Checked, 0);
                saved++;
            }

            SetStatus($"Сохранено файлов: {saved}.", OpencodeTheme.Success);
            State.Log(LogLevel.Success, $"Резервные копии: {saved} файл(ов) в {dialog.SelectedPath}.");
            ThemedMessageBox.Success(this, "Резервное копирование", $"Сохранено файлов: {saved}.\n{dialog.SelectedPath}");
        }
        catch (Exception ex)
        {
            SetStatus("Ошибка: " + ex.Message, OpencodeTheme.Error);
            State.Log(LogLevel.Error, $"Ошибка экспорта: {ex.Message}");
            ThemedMessageBox.Error(this, "Резервное копирование", ex.Message);
        }
    }

    private void UploadToCloud()
    {
        if (SelectedProfile is not { } profile)
        {
            ThemedMessageBox.Warning(this, "Облако", "Выберите подключение.");
            return;
        }

        var request = new CloudBackupRequest
        {
            Profile = profile,
            Tables = SelectedTables(),
            IncludeData = _includeData.Checked,
        };

        using var dialog = new CloudBackupDialog(State.Services.Cloud, request);
        dialog.ShowDialog(this);

        if (dialog.Result is { Success: true } result)
        {
            SetStatus("Облачная копия создана.", OpencodeTheme.Success);
            State.Log(LogLevel.Success, result.Message);
        }
        else if (dialog.Result is { } failed)
        {
            SetStatus("Загрузка не удалась.", OpencodeTheme.Error);
            State.Log(LogLevel.Error, failed.Message);
        }
    }

    private static string Sanitize(string name) =>
        string.Concat(name.Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '_' : c));

    private void SetStatus(string text, Color color)
    {
        _status.Text = text;
        _status.GlowColor = color;
    }
}
