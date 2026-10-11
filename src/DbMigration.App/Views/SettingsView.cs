using System.Drawing;
using System.Globalization;
using System.Windows.Forms;

namespace DbMigration.App.Views;

public sealed class SettingsView : ViewBase
{
    private readonly ToggleSwitch _animations = new();
    private readonly ThemedTextBox _fps = ViewUi.TextBox();
    private readonly ThemedTextBox _pageSize = ViewUi.TextBox();
    private readonly ThemedTextBox _maxQueryRows = ViewUi.TextBox();
    private readonly ThemedTextBox _batchSize = ViewUi.TextBox();
    private readonly ThemedTextBox _backupFolder = ViewUi.TextBox();
    private readonly ThemedTextBox _cloudEndpoint = ViewUi.TextBox();
    private readonly GlowLabel _status = new() { Font = UiFonts.Body, AutoSize = true, Animate = false, GlowRadius = 5, MaxIntensity = 0.5f };

    public SettingsView(AppState state)
        : base(state)
    {
        _fps.Width = 160;
        _pageSize.Width = 160;
        _maxQueryRows.Width = 160;
        _batchSize.Width = 160;
        _backupFolder.Width = 330;
        _cloudEndpoint.Width = 330;

        LoadValues();

        var save = ViewUi.Button("Сохранить настройки", ButtonVariant.Primary, 220);
        save.Click += (_, _) => Save();

        var browse = ViewUi.Button("Обзор…", ButtonVariant.Default, 110);
        browse.Click += (_, _) => BrowseFolder();

        var interfaceCard = ViewUi.Card("Интерфейс");
        var ifFlow = NewFlow();
        ifFlow.Controls.Add(MakeToggleRow("Анимация: свечение и пульсация текста", _animations));
        ifFlow.Controls.Add(ViewUi.Label("Кадров в секунду (15–120)", OpencodeTheme.TextMuted, UiFonts.BodySmall));
        ifFlow.Controls.Add(_fps);
        interfaceCard.Controls.Add(ifFlow);

        var dataCard = ViewUi.Card("Данные и миграция");
        var dataFlow = NewFlow();
        dataFlow.Controls.Add(ViewUi.Label("Строк на страницу в просмотре данных", OpencodeTheme.TextMuted, UiFonts.BodySmall));
        dataFlow.Controls.Add(_pageSize);
        dataFlow.Controls.Add(ViewUi.Label("Максимум строк в SQL-запросе", OpencodeTheme.TextMuted, UiFonts.BodySmall));
        dataFlow.Controls.Add(_maxQueryRows);
        dataFlow.Controls.Add(ViewUi.Label("Размер пакета миграции", OpencodeTheme.TextMuted, UiFonts.BodySmall));
        dataFlow.Controls.Add(_batchSize);
        dataCard.Controls.Add(dataFlow);

        var backupCard = ViewUi.Card("Резервное копирование и облако");
        var backupFlow = NewFlow();
        backupFlow.Controls.Add(ViewUi.Label("Папка резервных копий", OpencodeTheme.TextMuted, UiFonts.BodySmall));
        var folderRow = new FlowLayoutPanel
        {
            AutoSize = true,
            WrapContents = false,
            BackColor = Color.Transparent,
        };
        folderRow.Controls.Add(_backupFolder);
        folderRow.Controls.Add(browse);
        backupFlow.Controls.Add(folderRow);
        backupFlow.Controls.Add(ViewUi.Label("Облачный узел (имитация)", OpencodeTheme.TextMuted, UiFonts.BodySmall));
        backupFlow.Controls.Add(_cloudEndpoint);
        backupCard.Controls.Add(backupFlow);

        var leftHost = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            BackColor = OpencodeTheme.Background,
        };
        leftHost.RowStyles.Add(new RowStyle(SizeType.Percent, 45f));
        leftHost.RowStyles.Add(new RowStyle(SizeType.Percent, 55f));
        leftHost.Controls.Add(interfaceCard, 0, 0);
        leftHost.Controls.Add(dataCard, 0, 1);

        var rightHost = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            BackColor = OpencodeTheme.Background,
        };
        rightHost.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
        rightHost.RowStyles.Add(new RowStyle(SizeType.Absolute, 110f));
        rightHost.Controls.Add(backupCard, 0, 0);
        rightHost.Controls.Add(BuildSaveArea(save), 0, 1);

        var body = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            BackColor = OpencodeTheme.Background,
        };
        body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));
        body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));
        body.Controls.Add(leftHost, 0, 0);
        body.Controls.Add(rightHost, 1, 0);

        Controls.Add(body);
        Controls.Add(ViewUi.Header("Настройки", "Параметры анимации, данных и резервного копирования."));
    }

    public override string ViewTitle => "Настройки";

    public override Task ActivateAsync()
    {
        LoadValues();
        return Task.CompletedTask;
    }

    private static FlowLayoutPanel NewFlow() => new()
    {
        Dock = DockStyle.Fill,
        FlowDirection = FlowDirection.TopDown,
        WrapContents = false,
        AutoScroll = true,
        BackColor = Color.Transparent,
    };

    private Control BuildSaveArea(ThemedButton save)
    {
        var card = new Card();
        var flow = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoScroll = true,
            BackColor = Color.Transparent,
        };
        flow.Controls.Add(save);
        flow.Controls.Add(_status);
        card.Controls.Add(flow);
        return card;
    }

    private static Control MakeToggleRow(string text, ToggleSwitch toggle)
    {
        var panel = new Panel { Width = 420, Height = 34, BackColor = Color.Transparent };
        var label = new Label
        {
            Text = text,
            ForeColor = OpencodeTheme.Text,
            Font = UiFonts.Body,
            AutoSize = true,
            Location = new Point(0, 6),
        };
        toggle.SetBounds(336, 4, 48, 26);
        panel.Controls.Add(toggle);
        panel.Controls.Add(label);
        return panel;
    }

    private void LoadValues()
    {
        var settings = State.Settings;
        _animations.Checked = settings.AnimationsEnabled;
        _fps.Text = settings.AnimationFps.ToString(CultureInfo.InvariantCulture);
        _pageSize.Text = settings.PageSize.ToString(CultureInfo.InvariantCulture);
        _maxQueryRows.Text = settings.MaxQueryRows.ToString(CultureInfo.InvariantCulture);
        _batchSize.Text = settings.MigrationBatchSize.ToString(CultureInfo.InvariantCulture);
        _backupFolder.Text = settings.BackupFolder;
        _cloudEndpoint.Text = settings.CloudEndpoint;
    }

    private void BrowseFolder()
    {
        using var dialog = new FolderBrowserDialog
        {
            Description = "Папка для резервных копий",
            UseDescriptionForTitle = true,
            SelectedPath = _backupFolder.Text,
        };

        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            _backupFolder.Text = dialog.SelectedPath;
        }
    }

    private void Save()
    {
        var settings = State.Settings;
        settings.AnimationsEnabled = _animations.Checked;
        settings.AnimationFps = Clamp(_fps, 15, 120, 60);
        settings.PageSize = Clamp(_pageSize, 10, 5000, 200);
        settings.MaxQueryRows = Clamp(_maxQueryRows, 100, 250000, 5000);
        settings.MigrationBatchSize = Clamp(_batchSize, 1, 100000, 500);
        settings.BackupFolder = _backupFolder.Text.Trim();
        settings.CloudEndpoint = _cloudEndpoint.Text.Trim();

        State.Services.Settings.Save();

        var clock = AnimationClock.Instance;
        clock.Fps = settings.AnimationFps;
        if (settings.AnimationsEnabled)
        {
            clock.Start();
        }
        else
        {
            clock.Stop();
        }

        _status.Text = "Настройки сохранены.";
        _status.GlowColor = OpencodeTheme.Success;
        State.Log(LogLevel.Success, "Настройки сохранены.");
    }

    private static int Clamp(ThemedTextBox box, int min, int max, int fallback)
    {
        return int.TryParse(box.Text.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int value)
            ? Math.Clamp(value, min, max)
            : fallback;
    }
}