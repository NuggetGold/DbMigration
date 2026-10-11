using System.Drawing;
using System.Globalization;
using System.Windows.Forms;

namespace DbMigration.App.Dialogs;

public sealed class ExportDialog : ThemedDialog
{
    private readonly AppState _state;
    private readonly IReadOnlyList<TableSchema> _tables;
    private readonly ThemedComboBox _tableCombo = new();
    private readonly ThemedComboBox _formatCombo = new();
    private readonly ToggleSwitch _includeData = new();
    private readonly ThemedTextBox _maxRows = new();
    private readonly ThemedTextBox _output = new();
    private readonly ThemedButton _browse = new() { Text = "Обзор…", Variant = ButtonVariant.Default, Width = 96, Height = 34 };
    private readonly Panel _body;
    private readonly ExportFormat[] _formats = { ExportFormat.SqlScript, ExportFormat.Csv, ExportFormat.Json };
    private int _y;

    public ExportDialog(AppState state, IReadOnlyList<TableSchema> tables, string initialTableName = "")
    {
        _state = state;
        _tables = tables;
        TitleText = "Экспорт данных";
        Size = new Size(660, 580);

        _body = new Panel
        {
            Dock = DockStyle.Fill,
            AutoScroll = true,
            BackColor = OpencodeTheme.Background,
            Padding = new Padding(24, 16, 24, 16),
        };

        _tableCombo.SetItems(_tables.Select(t => (object)t.QualifiedName));
        int initial = 0;
        for (int i = 0; i < _tables.Count; i++)
        {
            if (string.Equals(_tables[i].QualifiedName, initialTableName, StringComparison.OrdinalIgnoreCase))
            {
                initial = i;
            }
        }

        _tableCombo.SelectedIndex = _tables.Count > 0 ? initial : -1;

        _formatCombo.SetItems(_formats.Select(f => (object)_state.Services.Exporter.GetDisplayName(f)));
        _formatCombo.SelectedIndex = 0;
        _formatCombo.SelectedIndexChanged += (_, _) => UpdateSuggestedName();

        _maxRows.Text = _state.Settings.MaxQueryRows.ToString(CultureInfo.InvariantCulture);
        _output.Placeholder = "C:\\путь\\к\\файлу";
        _browse.Click += (_, _) => Browse();
        _includeData.Checked = true;

        var export = new ThemedButton { Text = "Экспортировать", Variant = ButtonVariant.Primary, Width = 170, Height = 34 };
        export.Click += (_, _) => TryAccept();
        var cancel = GhostButton("Отмена");

        ContentPanel.Controls.Add(_body);
        ContentPanel.Controls.Add(CreateFooter(export, cancel));
        AcceptButton = export;
        CancelButton = cancel;

        BuildFields();
    }

    public TableSchema? SelectedTable { get; private set; }

    public ExportFormat Format { get; private set; }

    public bool IncludeData { get; private set; }

    public string OutputPath { get; private set; } = string.Empty;

    public int MaxRows { get; private set; }

    private void BuildFields()
    {
        _body.Controls.Clear();
        _y = 0;
        AddField("Таблица", _tableCombo);
        AddField("Формат", _formatCombo);
        AddField("Максимум строк", _maxRows);
        AddToggle("Включить данные", _includeData);

        var row = new Panel { BackColor = Color.Transparent };
        row.SetBounds(0, _y, 560, 34);
        _output.SetBounds(0, 0, 452, 34);
        _browse.SetBounds(464, 0, 96, 34);
        row.Controls.Add(_browse);
        row.Controls.Add(_output);
        AddRaw("Файл назначения", row, 34);
        UpdateSuggestedName();
    }

    private void UpdateSuggestedName()
    {
        if (_output.Text.Length > 0)
        {
            return;
        }

        string table = (_tableCombo.SelectedItem as string) ?? "table";
        string extension = _state.Services.Exporter.GetExtension(CurrentFormat);
        _output.Text = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            $"{table.Replace('.', '_')}.{extension}");
    }

    private ExportFormat CurrentFormat => _formats[Math.Max(0, _formatCombo.SelectedIndex)];

    private void AddField(string label, Control field) => AddRaw(label, field, 34);

    private void AddToggle(string label, ToggleSwitch toggle)
    {
        var row = new Panel { BackColor = Color.Transparent };
        row.SetBounds(0, _y, 560, 34);
        var lbl = new Label
        {
            Text = label,
            ForeColor = OpencodeTheme.Text,
            Font = UiFonts.Body,
            AutoSize = true,
            Location = new Point(0, 6),
        };
        toggle.SetBounds(512, 4, 48, 26);
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
            lbl.SetBounds(0, _y, 560, 18);
            _body.Controls.Add(lbl);
            _y += 20;
        }

        field.SetBounds(0, _y, 560, fieldHeight);
        _body.Controls.Add(field);
        _y += fieldHeight + 14;
    }

    private void Browse()
    {
        ExportFormat format = CurrentFormat;
        string extension = _state.Services.Exporter.GetExtension(format);
        using var dialog = new SaveFileDialog
        {
            Title = "Куда сохранить экспорт",
            Filter = $"Файлы {extension.ToUpperInvariant()} (*.{extension})|*.{extension}|Все файлы (*.*)|*.*",
            FileName = Path.GetFileName(_output.Text),
            OverwritePrompt = true,
        };

        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            _output.Text = dialog.FileName;
        }
    }

    private void TryAccept()
    {
        if (_tableCombo.SelectedIndex < 0 || _tableCombo.SelectedIndex >= _tables.Count)
        {
            ThemedMessageBox.Warning(this, "Проверка", "Выберите таблицу для экспорта.");
            return;
        }

        if (_output.Text.Trim().Length == 0)
        {
            ThemedMessageBox.Warning(this, "Проверка", "Укажите файл назначения.");
            return;
        }

        SelectedTable = _tables[_tableCombo.SelectedIndex];
        Format = CurrentFormat;
        IncludeData = _includeData.Checked;
        OutputPath = _output.Text.Trim();
        MaxRows = int.TryParse(_maxRows.Text.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int rows)
            ? Math.Max(1, rows)
            : _state.Settings.MaxQueryRows;
        DialogResult = DialogResult.OK;
    }
}
