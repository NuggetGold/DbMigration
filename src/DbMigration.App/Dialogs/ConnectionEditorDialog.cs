using System.Drawing;
using System.Windows.Forms;

namespace DbMigration.App.Dialogs;

public sealed class ConnectionEditorDialog : ThemedDialog
{
    private readonly ThemedTextBox _nameBox = new();
    private readonly ThemedComboBox _engineCombo = new();
    private readonly ThemedTextBox _fileBox = new();
    private readonly ThemedButton _browseButton = new() { Text = "Обзор…", Variant = ButtonVariant.Default, Width = 96, Height = 34 };
    private readonly ThemedTextBox _hostBox = new();
    private readonly ThemedTextBox _portBox = new();
    private readonly ThemedTextBox _databaseBox = new();
    private readonly ThemedTextBox _userBox = new();
    private readonly ThemedTextBox _passwordBox = new();
    private readonly ToggleSwitch _integratedToggle = new();
    private readonly ToggleSwitch _trustToggle = new();
    private readonly Panel _fieldsPanel;
    private readonly ConnectionProfile _profile;
    private int _y;

    private static readonly DatabaseEngine[] Engines =
    {
        DatabaseEngine.Sqlite,
        DatabaseEngine.MySql,
        DatabaseEngine.PostgreSql,
        DatabaseEngine.SqlServer,
    };

    public ConnectionEditorDialog(ConnectionProfile? existing = null)
    {
        _profile = existing?.Clone() ?? new ConnectionProfile { Name = "Новое подключение" };
        TitleText = existing is null ? "Новое подключение" : "Изменение подключения";
        Size = new Size(560, 620);

        _fieldsPanel = new Panel
        {
            Dock = DockStyle.Fill,
            AutoScroll = true,
            BackColor = OpencodeTheme.Background,
            Padding = new Padding(24, 16, 24, 16),
        };

        _nameBox.Placeholder = "Название подключения";
        _fileBox.Placeholder = "C:\\путь\\к\\базе.db";
        _hostBox.Placeholder = "localhost";
        _portBox.Placeholder = "по умолчанию";
        _databaseBox.Placeholder = "имя базы данных";
        _userBox.Placeholder = "пользователь";
        _passwordBox.Placeholder = "пароль";
        _passwordBox.Editor.UseSystemPasswordChar = true;

        _engineCombo.SetItems(Engines.Select(EngineName));
        _engineCombo.SelectedIndexChanged += (_, _) => RebuildFields();
        _browseButton.Click += (_, _) => BrowseForFile();

        var save = new ThemedButton { Text = "Сохранить", Variant = ButtonVariant.Primary, Width = 140, Height = 34 };
        save.Click += (_, _) => TrySave();
        var cancel = GhostButton("Отмена");

        ContentPanel.Controls.Add(_fieldsPanel);
        ContentPanel.Controls.Add(CreateFooter(save, cancel));
        AcceptButton = save;
        CancelButton = cancel;

        LoadFrom(_profile);
    }

    public ConnectionProfile? Result { get; private set; }

    private void LoadFrom(ConnectionProfile profile)
    {
        _nameBox.Text = profile.Name;
        int index = Array.IndexOf(Engines, profile.Engine);
        _engineCombo.SelectedIndex = index < 0 ? 0 : index;
        _fileBox.Text = profile.FilePath ?? string.Empty;
        _hostBox.Text = profile.Host ?? string.Empty;
        _portBox.Text = profile.Port?.ToString() ?? string.Empty;
        _databaseBox.Text = profile.Database ?? string.Empty;
        _userBox.Text = profile.User ?? string.Empty;
        _passwordBox.Text = profile.Password ?? string.Empty;
        _integratedToggle.Checked = profile.IntegratedSecurity;
        _trustToggle.Checked = profile.TrustServerCertificate;
        RebuildFields();
    }

    private DatabaseEngine SelectedEngine => Engines[Math.Max(0, _engineCombo.SelectedIndex)];

    private static string EngineName(DatabaseEngine engine) => engine switch
    {
        DatabaseEngine.Sqlite => "SQLite",
        DatabaseEngine.MySql => "MySQL",
        DatabaseEngine.PostgreSql => "PostgreSQL",
        _ => "SQL Server",
    };

    private void RebuildFields()
    {
        _fieldsPanel.Controls.Clear();
        _y = 0;
        AddField("Название", _nameBox);
        AddField("СУБД", _engineCombo);

        if (SelectedEngine == DatabaseEngine.Sqlite)
        {
            var row = new Panel { BackColor = Color.Transparent };
            row.SetBounds(0, _y, 480, 34);
            _fileBox.SetBounds(0, 0, 372, 34);
            _browseButton.SetBounds(384, 0, 96, 34);
            row.Controls.Add(_browseButton);
            row.Controls.Add(_fileBox);
            AddRaw("Файл базы данных", row, 34);
        }
        else
        {
            AddField("Хост", _hostBox);
            AddField("Порт", _portBox);
            AddField("База данных", _databaseBox);
            AddField("Пользователь", _userBox);

            if (SelectedEngine == DatabaseEngine.SqlServer)
            {
                AddToggle("Встроенная проверка подлинности Windows", _integratedToggle);
            }

            AddField("Пароль", _passwordBox);

            if (SelectedEngine is DatabaseEngine.SqlServer or DatabaseEngine.MySql)
            {
                AddToggle("Доверять сертификату сервера", _trustToggle);
            }
        }
    }

    private void AddField(string label, Control field)
    {
        AddRaw(label, field, 34);
    }

    private void AddToggle(string label, ToggleSwitch toggle)
    {
        var row = new Panel { BackColor = Color.Transparent };
        row.SetBounds(0, _y, 480, 34);
        var lbl = new Label
        {
            Text = label,
            ForeColor = OpencodeTheme.Text,
            Font = UiFonts.Body,
            AutoSize = true,
            Location = new Point(0, 6),
        };
        toggle.SetBounds(400, 4, 48, 26);
        row.Controls.Add(toggle);
        row.Controls.Add(lbl);
        AddRaw(string.Empty, row, 34);
    }

    private void AddRaw(string label, Control field, int fieldHeight)
    {
        if (label.Length > 0)
        {
            var lbl = new Label
            {
                Text = label,
                ForeColor = OpencodeTheme.TextMuted,
                Font = UiFonts.BodySmall,
            };
            lbl.SetBounds(0, _y, 480, 18);
            _fieldsPanel.Controls.Add(lbl);
            _y += 20;
        }

        field.SetBounds(0, _y, 480, fieldHeight);
        _fieldsPanel.Controls.Add(field);
        _y += fieldHeight + 14;
    }

    private void BrowseForFile()
    {
        using var dialog = new SaveFileDialog
        {
            Title = "Выбор файла базы SQLite",
            Filter = "Базы SQLite (*.db;*.sqlite;*.sqlite3;*.db3)|*.db;*.sqlite;*.sqlite3;*.db3|Все файлы (*.*)|*.*",
            FileName = _fileBox.Text,
            OverwritePrompt = false,
        };

        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            _fileBox.Text = dialog.FileName;
        }
    }

    private void TrySave()
    {
        string name = _nameBox.Text.Trim();
        if (name.Length == 0)
        {
            ThemedMessageBox.Warning(this, "Проверка", "Введите название подключения.");
            return;
        }

        var profile = _profile;
        profile.Name = name;
        profile.Engine = SelectedEngine;
        profile.FilePath = _fileBox.Text.Trim();
        profile.Host = _hostBox.Text.Trim();
        profile.Port = int.TryParse(_portBox.Text.Trim(), out int port) ? port : null;
        profile.Database = _databaseBox.Text.Trim();
        profile.User = _userBox.Text.Trim();
        profile.Password = _passwordBox.Text;
        profile.IntegratedSecurity = _integratedToggle.Checked;
        profile.TrustServerCertificate = _trustToggle.Checked;

        if (SelectedEngine == DatabaseEngine.Sqlite && profile.FilePath.Length == 0)
        {
            ThemedMessageBox.Warning(this, "Проверка", "Выберите файл базы данных.");
            return;
        }

        if (SelectedEngine != DatabaseEngine.Sqlite && (profile.Host!.Length == 0 || profile.Database!.Length == 0))
        {
            ThemedMessageBox.Warning(this, "Проверка", "Укажите хост и базу данных.");
            return;
        }

        Result = profile;
        DialogResult = DialogResult.OK;
    }
}
