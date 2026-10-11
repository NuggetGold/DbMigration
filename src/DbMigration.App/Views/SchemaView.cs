using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace DbMigration.App.Views;

public sealed class SchemaView : ViewBase
{
    private readonly ThemedComboBox _profileCombo = new() { Width = 240, Placeholder = "Подключение" };
    private readonly ThemedTextBox _search = ViewUi.TextBox("Поиск по таблицам и столбцам");
    private readonly ThemedTreeView _tree = new() { Dock = DockStyle.Fill };
    private readonly GlowLabel _detailTitle = new() { Font = UiFonts.DisplayLarge, GlowColor = OpencodeTheme.Secondary, AutoSize = true };
    private readonly Label _detailInfo = ViewUi.Label(string.Empty, OpencodeTheme.Text);
    private readonly ThemedTextBox _script = new() { Multiline = true, ReadOnly = true, UseMonoFont = true, Dock = DockStyle.Fill };
    private readonly ThemedButton _reload = ViewUi.Button("Обновить");
    private readonly ThemedButton _export = ViewUi.Button("Экспорт таблицы…");
    private readonly ThemedButton _copy = ViewUi.Button("Копировать DDL", ButtonVariant.Ghost, 150);
    private IReadOnlyList<TableSchema> _schema = Array.Empty<TableSchema>();
    private List<ConnectionProfile> _profiles = new();

    public SchemaView(AppState state)
        : base(state)
    {
        _search.Width = 260;
        _profileCombo.SelectedIndexChanged += async (_, _) => await LoadSchemaAsync();
        _reload.Click += async (_, _) => await LoadSchemaAsync();
        _search.TextChanged += (_, _) => RebuildTree();
        _tree.AfterSelect += (_, e) => ShowSelected(e.Node);
        _export.Click += (_, _) => ExportSelected();
        _copy.Click += (_, _) =>
        {
            if (_script.Text.Length == 0)
            {
                return;
            }

            try
            {
                Clipboard.SetText(_script.Text);
            }
            catch (ExternalException ex)
            {
                State.Log(LogLevel.Error, $"Не удалось скопировать: {ex.Message}");
            }
        };

        var left = new Card { Padding = new Padding(10, 12, 10, 10) };
        left.Controls.Add(_tree);

        var right = ViewUi.Card("Определение таблицы");
        var rightFlow = new Panel { Dock = DockStyle.Fill, BackColor = Color.Transparent };
        var head = new Panel { Dock = DockStyle.Top, Height = 54, BackColor = Color.Transparent };
        _detailTitle.Location = new Point(0, 0);
        head.Controls.Add(_detailTitle);
        _detailInfo.Location = new Point(0, 30);
        head.Controls.Add(_detailInfo);
        rightFlow.Controls.Add(_script);
        rightFlow.Controls.Add(head);
        right.Controls.Add(rightFlow);

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
        Controls.Add(ViewUi.Toolbar(_profileCombo, _reload, _search, _export, _copy));
        Controls.Add(ViewUi.Header("Схема", "Просмотр структуры таблиц и глобальный поиск по таблицам и столбцам."));

        State.ProfilesChanged += (_, _) => ReloadProfiles();
        ReloadProfiles();
    }

    public override string ViewTitle => "Схема";

    public override Task ActivateAsync()
    {
        ReloadProfiles();
        return Task.CompletedTask;
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

    private async Task LoadSchemaAsync()
    {
        if (SelectedProfile is not { } profile)
        {
            _schema = Array.Empty<TableSchema>();
            RebuildTree();
            return;
        }

        _detailInfo.Text = "Загрузка схемы…";
        try
        {
            var provider = State.Services.Providers.Get(profile.Engine);
            _schema = await provider.GetSchemaAsync(profile);
            RebuildTree();
            State.Log(LogLevel.Info, $"Схема «{profile.Name}»: таблиц — {_schema.Count}.");
        }
        catch (Exception ex)
        {
            _schema = Array.Empty<TableSchema>();
            RebuildTree();
            _detailInfo.Text = "Ошибка: " + ex.Message;
            State.Log(LogLevel.Error, $"Не удалось прочитать схему «{profile.Name}»: {ex.Message}");
        }
    }

    private void RebuildTree()
    {
        string query = (_search.Text ?? string.Empty).Trim();
        _tree.BeginUpdate();
        _tree.Nodes.Clear();

        int tablesShown = 0;
        foreach (var table in _schema)
        {
            bool tableMatch = query.Length == 0 || FuzzyMatcher.Score(query, table.Name) >= 0;
            var columns = query.Length == 0
                ? table.Columns
                : table.Columns.Where(c => tableMatch || FuzzyMatcher.Score(query, c.Name) >= 0).ToList();

            if (query.Length > 0 && !tableMatch && columns.Count == 0)
            {
                continue;
            }

            tablesShown++;
            var node = new TreeNode($"{table.QualifiedName}   ({table.Columns.Count})")
            {
                Tag = table,
                ForeColor = OpencodeTheme.Secondary,
            };

            foreach (var column in table.Columns.OrderBy(c => c.Ordinal))
            {
                string flags = column.IsPrimaryKey ? "  PK" : column.IsNullable ? string.Empty : "  NOT NULL";
                string marker = columns.Contains(column) && query.Length > 0 ? "● " : string.Empty;
                var child = new TreeNode($"{marker}{column.Name} : {column.NativeType}{flags}")
                {
                    Tag = column,
                    ForeColor = column.IsPrimaryKey ? OpencodeTheme.Primary : OpencodeTheme.Text,
                };
                node.Nodes.Add(child);
            }

            _tree.Nodes.Add(node);
        }

        _tree.EndUpdate();
        _detailInfo.Text = _schema.Count == 0
            ? "Нет данных. Выберите подключение и нажмите «Обновить»."
            : $"Показано таблиц: {tablesShown} из {_schema.Count}.";
    }

    private void ShowSelected(TreeNode? node)
    {
        if (node?.Tag is not TableSchema table)
        {
            return;
        }

        _detailTitle.Text = table.QualifiedName;
        _detailInfo.Text =
            $"Столбцов: {table.Columns.Count}   ·   Индексов: {table.Indexes.Count}   ·   Связей: {table.ForeignKeys.Count}   ·   Строк: {table.RowCount?.ToString() ?? "неизвестно"}";

        if (SelectedProfile is { } profile)
        {
            var provider = State.Services.Providers.Get(profile.Engine);
            _script.Text = provider.BuildCreateTableSql(table, true);
        }
    }

    private async void ExportSelected()
    {
        if (_tree.SelectedNode?.Tag is not TableSchema table)
        {
            ThemedMessageBox.Info(this, "Экспорт", "Выберите таблицу в дереве.");
            return;
        }

        if (SelectedProfile is not { } profile)
        {
            return;
        }

        using var dialog = new ExportDialog(State, new[] { table }, table.QualifiedName);
        if (dialog.ShowDialog(this) != DialogResult.OK || dialog.SelectedTable is not { } chosen)
        {
            return;
        }

        try
        {
            await using var stream = File.Create(dialog.OutputPath);
            await State.Services.Exporter.ExportTableAsync(
                profile,
                chosen,
                dialog.Format,
                stream,
                dialog.IncludeData,
                dialog.MaxRows);
            State.Log(LogLevel.Success, $"Экспорт «{chosen.QualifiedName}» → {dialog.OutputPath}.");
            ThemedMessageBox.Success(this, "Экспорт", $"Файл сохранён:\n{dialog.OutputPath}");
        }
        catch (Exception ex)
        {
            State.Log(LogLevel.Error, $"Ошибка экспорта: {ex.Message}");
            ThemedMessageBox.Error(this, "Экспорт", ex.Message);
        }
    }
}
