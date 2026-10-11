using System.Drawing;
using System.Globalization;
using System.Windows.Forms;

namespace DbMigration.App.Views;

public sealed class DataView : ViewBase
{
    private readonly ThemedComboBox _profileCombo = new() { Width = 200, Placeholder = "Подключение" };
    private readonly ThemedComboBox _tableCombo = new() { Width = 230, Placeholder = "Таблица" };
    private readonly ThemedTextBox _pageSize = ViewUi.TextBox("строк");
    private readonly ThemedDataGridView _grid = new() { Dock = DockStyle.Fill };
    private readonly Label _pageLabel = ViewUi.Label("Стр. 1", OpencodeTheme.Text);
    private readonly GlowLabel _status = new() { Font = UiFonts.Body, AutoSize = true, Animate = false, GlowRadius = 5, MaxIntensity = 0.5f };
    private readonly ThemedButton _load = ViewUi.Button("Загрузить", ButtonVariant.Primary, 130);
    private readonly ThemedButton _prev = ViewUi.Button("Назад", ButtonVariant.Default, 100);
    private readonly ThemedButton _next = ViewUi.Button("Вперёд", ButtonVariant.Default, 100);
    private readonly ThemedButton _edit = ViewUi.Button("Режим правки", ButtonVariant.Ghost, 160);
    private readonly ThemedButton _save = ViewUi.Button("Сохранить", ButtonVariant.Primary, 150);
    private readonly ThemedButton _delete = ViewUi.Button("Удалить строку", ButtonVariant.Danger, 170);
    private List<ConnectionProfile> _profiles = new();
    private IReadOnlyList<TableSchema> _schemaCache = Array.Empty<TableSchema>();
    private IReadOnlyList<string> _columns = Array.Empty<string>();
    private List<object?[]> _rows = new();
    private TableSchema? _table;
    private int _page;
    private bool _editMode;

    public DataView(AppState state)
        : base(state)
    {
        _pageSize.Width = 80;
        _pageSize.Text = State.Settings.PageSize.ToString(CultureInfo.InvariantCulture);

        _profileCombo.SelectedIndexChanged += async (_, _) => await LoadTablesAsync();
        _tableCombo.SelectedIndexChanged += async (_, _) => await LoadPageAsync(reset: true);
        _load.Click += async (_, _) => await LoadPageAsync(reset: true);
        _prev.Click += async (_, _) =>
        {
            if (_page > 0)
            {
                _page--;
                await LoadPageAsync(reset: false);
            }
        };
        _next.Click += async (_, _) =>
        {
            _page++;
            await LoadPageAsync(reset: false);
        };
        _edit.Click += (_, _) =>
        {
            _editMode = !_editMode;
            _grid.ReadOnly = !_editMode;
            _edit.Active = _editMode;
            _status.Text = _editMode ? "Режим правки включён. Изменяйте ячейки и нажмите «Сохранить»." : "Режим просмотра.";
            _status.GlowColor = _editMode ? OpencodeTheme.Warning : OpencodeTheme.TextMuted;
        };
        _save.Click += async (_, _) => await SaveChangesAsync();
        _delete.Click += async (_, _) => await DeleteRowAsync();

        var gridHost = ViewUi.Card();
        gridHost.Controls.Add(_grid);

        var navToolbar = ViewUi.Toolbar(
            ViewUi.Label("Строк на страницу:", OpencodeTheme.TextMuted, UiFonts.BodySmall),
            _pageSize,
            _prev,
            _next,
            _pageLabel);
        navToolbar.Padding = new Padding(0, 4, 0, 4);

        var editToolbar = ViewUi.Toolbar(_edit, _save, _delete, _status);
        editToolbar.Padding = new Padding(0, 4, 0, 6);

        Controls.Add(gridHost);
        Controls.Add(editToolbar);
        Controls.Add(navToolbar);
        Controls.Add(ViewUi.Toolbar(_profileCombo, _tableCombo, _load));
        Controls.Add(ViewUi.Header("Данные", "Просмотр и редактирование строк таблицы постранично."));

        State.ProfilesChanged += (_, _) => ReloadProfiles();
        ReloadProfiles();
    }

    public override string ViewTitle => "Данные";

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

    private async Task LoadTablesAsync()
    {
        _tableCombo.SetItems(Array.Empty<object>());
        _schemaCache = Array.Empty<TableSchema>();
        _table = null;

        if (SelectedProfile is not { } profile)
        {
            return;
        }

        try
        {
            var provider = State.Services.Providers.Get(profile.Engine);
            var names = await State.Services.Migration.GetTableNamesAsync(profile);
            _schemaCache = await provider.GetSchemaAsync(profile);
            _tableCombo.SetItems(names.Cast<object>());
            if (names.Count > 0)
            {
                _tableCombo.SelectedIndex = 0;
            }
        }
        catch (Exception ex)
        {
            _status.Text = "Ошибка: " + ex.Message;
            _status.GlowColor = OpencodeTheme.Error;
            State.Log(LogLevel.Error, $"Не удалось получить список таблиц: {ex.Message}");
        }
    }

    private async Task LoadPageAsync(bool reset)
    {
        if (reset)
        {
            _page = 0;
        }

        if (SelectedProfile is not { } profile || _tableCombo.SelectedIndex < 0)
        {
            return;
        }

        string tableName = _tableCombo.SelectedItem?.ToString() ?? string.Empty;
        _table = _schemaCache.FirstOrDefault(t => string.Equals(t.Name, tableName, StringComparison.OrdinalIgnoreCase));

        int pageSize = int.TryParse(_pageSize.Text.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int size)
            ? Math.Clamp(size, 10, 5000)
            : State.Settings.PageSize;

        try
        {
            var provider = State.Services.Providers.Get(profile.Engine);
            string target = Quote(provider, tableName);
            int take = (_page + 1) * pageSize;
            QueryResult result = await provider.ExecuteQueryAsync(profile, $"SELECT * FROM {target}", take);

            _columns = result.Columns;
            var all = result.Rows;
            _rows = all.Skip(_page * pageSize).Take(pageSize).ToList();

            _grid.ReadOnly = !_editMode;
            _grid.LoadResult(new QueryResult
            {
                Columns = _columns,
                Rows = _rows,
                HasRows = _rows.Count > 0,
            });

            _pageLabel.Text = $"Стр. {_page + 1}";
            _prev.Enabled = _page > 0;
            _next.Enabled = _rows.Count >= pageSize;
            _status.Text = $"Загружено строк: {_rows.Count}. Всего получено: {all.Count}.";
            _status.GlowColor = OpencodeTheme.Info;
        }
        catch (Exception ex)
        {
            _status.Text = "Ошибка: " + ex.Message;
            _status.GlowColor = OpencodeTheme.Error;
            State.Log(LogLevel.Error, $"Ошибка чтения данных: {ex.Message}");
        }
    }

    private async Task SaveChangesAsync()
    {
        if (!_editMode || SelectedProfile is not { } profile || _table is not { } table)
        {
            return;
        }

        var keys = table.PrimaryKey;
        if (keys.Count == 0)
        {
            ThemedMessageBox.Warning(this, "Правка", "Таблица не имеет первичного ключа — построчное сохранение недоступно.");
            return;
        }

        var provider = State.Services.Providers.Get(profile.Engine);
        int updates = 0;

        try
        {
            for (int r = 0; r < _rows.Count && r < _grid.Rows.Count; r++)
            {
                var changed = new List<(string Column, object? Value)>();
                for (int c = 0; c < _columns.Count; c++)
                {
                    object? current = _grid.Rows[r].Cells[c].Value is DBNull ? null : _grid.Rows[r].Cells[c].Value;
                    object? original = _rows[r][c];
                    if (current?.ToString() != original?.ToString())
                    {
                        changed.Add((_columns[c], current));
                    }
                }

                if (changed.Count == 0)
                {
                    continue;
                }

                string set = string.Join(", ", changed.Select(x => $"{provider.QuoteIdentifier(x.Column)} = {Literal(x.Value)}"));
                string where = string.Join(" AND ", keys.Select(k => $"{provider.QuoteIdentifier(k.Name)} = {Literal(OriginalValue(k.Name, r))}"));
                string sql = $"UPDATE {Quote(provider, table.Name)} SET {set} WHERE {where}";
                await provider.ExecuteNonQueryAsync(profile, sql);
                updates++;
            }

            if (updates > 0)
            {
                State.Log(LogLevel.Success, $"Сохранено изменённых строк: {updates}.");
                ThemedMessageBox.Success(this, "Правка", $"Сохранено строк: {updates}.");
            }

            await LoadPageAsync(reset: false);
        }
        catch (Exception ex)
        {
            State.Log(LogLevel.Error, $"Ошибка сохранения: {ex.Message}");
            ThemedMessageBox.Error(this, "Правка", ex.Message);
        }
    }

    private async Task DeleteRowAsync()
    {
        if (SelectedProfile is not { } profile || _table is not { } table)
        {
            return;
        }

        if (_grid.CurrentRow is null || _grid.CurrentRow.Index < 0 || _grid.CurrentRow.Index >= _rows.Count)
        {
            return;
        }

        var keys = table.PrimaryKey;
        if (keys.Count == 0)
        {
            ThemedMessageBox.Warning(this, "Удаление", "Таблица не имеет первичного ключа — удаление недоступно.");
            return;
        }

        int index = _grid.CurrentRow.Index;
        if (!ThemedMessageBox.Confirm(this, "Удаление", $"Удалить строку №{index + 1} из «{table.Name}»?", destructive: true))
        {
            return;
        }

        var provider = State.Services.Providers.Get(profile.Engine);
        string where = string.Join(" AND ", keys.Select(k => $"{provider.QuoteIdentifier(k.Name)} = {Literal(OriginalValue(k.Name, index))}"));

        try
        {
            int affected = await provider.ExecuteNonQueryAsync(profile, $"DELETE FROM {Quote(provider, table.Name)} WHERE {where}");
            State.Log(LogLevel.Warning, $"Удалено строк: {affected}.");
            await LoadPageAsync(reset: false);
        }
        catch (Exception ex)
        {
            State.Log(LogLevel.Error, $"Ошибка удаления: {ex.Message}");
            ThemedMessageBox.Error(this, "Удаление", ex.Message);
        }
    }

    private object? OriginalValue(string column, int rowIndex)
    {
        int index = _columns.ToList().FindIndex(c => string.Equals(c, column, StringComparison.OrdinalIgnoreCase));
        return index >= 0 && index < _rows[rowIndex].Length ? _rows[rowIndex][index] : null;
    }

    private static string Quote(IDatabaseProvider provider, string table) =>
        string.Join(".", table.Split('.', StringSplitOptions.RemoveEmptyEntries).Select(provider.QuoteIdentifier));

    private static string Literal(object? value)
    {
        if (value is null or DBNull)
        {
            return "NULL";
        }

        return value switch
        {
            bool b => b ? "1" : "0",
            byte[] bytes => "X'" + Convert.ToHexString(bytes) + "'",
            DateTime dt => "'" + dt.ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture) + "'",
            DateTimeOffset dto => "'" + dto.ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture) + "'",
            TimeSpan ts => "'" + ts.ToString(@"hh\:mm\:ss", CultureInfo.InvariantCulture) + "'",
            Guid guid => "'" + guid + "'",
            sbyte or byte or short or ushort or int or uint or long or ulong or float or double or decimal =>
                Convert.ToString(value, CultureInfo.InvariantCulture) ?? "NULL",
            _ => "'" + value.ToString()!.Replace("'", "''", StringComparison.Ordinal) + "'",
        };
    }
}
