using System.Drawing;
using System.Windows.Forms;
using DbMigration.Core.Security;

namespace DbMigration.App.Views;

public sealed class SqlConsoleView : ViewBase
{
    private static readonly string[] RowKeywords = { "select", "with", "pragma", "show", "explain", "describe", "desc" };

    private readonly ThemedComboBox _profileCombo = new() { Width = 230, Placeholder = "Подключение" };
    private readonly ThemedComboBox _history = new() { Width = 300, Placeholder = "История запросов" };
    private readonly ThemedTextBox _editor = new() { Multiline = true, UseMonoFont = true, Dock = DockStyle.Fill };
    private readonly ThemedDataGridView _grid = new() { Dock = DockStyle.Fill };
    private readonly GlowLabel _status = new() { Font = UiFonts.Body, AutoSize = true, Animate = false, GlowRadius = 5, MaxIntensity = 0.5f };
    private readonly ThemedButton _run = ViewUi.Button("Выполнить", ButtonVariant.Primary, 160);
    private readonly ThemedButton _clear = ViewUi.Button("Очистить", ButtonVariant.Ghost, 130);
    private readonly List<string> _statements = new();
    private List<ConnectionProfile> _profiles = new();

    public SqlConsoleView(AppState state)
        : base(state)
    {
        _editor.EditorKeyDown += (_, e) =>
        {
            if (e.Control && e.KeyCode == Keys.Enter)
            {
                e.Handled = true;
                ExecuteSafely();
            }
        };
        _run.Click += (_, _) => ExecuteSafely();
        _clear.Click += (_, _) =>
        {
            _editor.Text = string.Empty;
            _grid.Columns.Clear();
            _grid.Rows.Clear();
            SetStatus("Готово. Введите SQL и нажмите Ctrl+Enter.", OpencodeTheme.TextMuted);
        };
        _history.SelectedIndexChanged += (_, _) =>
        {
            if (_history.SelectedIndex >= 0 && _history.SelectedIndex < _statements.Count)
            {
                _editor.Text = _statements[_history.SelectedIndex];
            }
        };

        var editorCard = ViewUi.Card("Запрос");
        editorCard.Controls.Add(_editor);
        editorCard.Height = 210;

        var gridCard = ViewUi.Card("Результат");
        gridCard.Controls.Add(_grid);

        var body = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            BackColor = OpencodeTheme.Background,
        };
        body.RowStyles.Add(new RowStyle(SizeType.Absolute, 220f));
        body.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
        body.Controls.Add(editorCard, 0, 0);
        body.Controls.Add(gridCard, 0, 1);

        Controls.Add(body);
        Controls.Add(ViewUi.Toolbar(_profileCombo, _history, _run, _clear, _status));
        Controls.Add(ViewUi.Header("SQL-консоль", "Выполнение произвольных запросов с защитой от опасных операторов."));

        State.ProfilesChanged += (_, _) => ReloadProfiles();
        ReloadProfiles();
    }

    public override string ViewTitle => "SQL-консоль";

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

    private async void ExecuteSafely()
    {
        try
        {
            await ExecuteAsync();
        }
        catch (Exception ex)
        {
            SetStatus("Ошибка: " + ex.Message, OpencodeTheme.Error);
            State.Log(LogLevel.Error, $"Ошибка SQL: {ex.Message}");
        }
    }

    private async Task ExecuteAsync()
    {
        if (SelectedProfile is not { } profile)
        {
            ThemedMessageBox.Warning(this, "SQL", "Выберите подключение.");
            return;
        }

        string sql = (_editor.Text ?? string.Empty).Trim();
        if (sql.Length == 0)
        {
            return;
        }

        SqlGuardVerdict verdict = SqlGuard.Evaluate(sql);
        if (verdict.RequiresConfirmation)
        {
            string title = verdict.Level == SqlGuardLevel.Destructive ? "Опасный запрос" : "Подтверждение";
            if (!ThemedMessageBox.Confirm(this, title, $"{verdict.Message}\n\nВсё равно выполнить?", destructive: verdict.IsDestructive))
            {
                SetStatus("Запрос отменён.", OpencodeTheme.Warning);
                return;
            }
        }

        Remember(sql);
        SetStatus("Выполняется…", OpencodeTheme.Info);

        try
        {
            var provider = State.Services.Providers.Get(profile.Engine);
            if (ReturnsRows(sql))
            {
                QueryResult result = await provider.ExecuteQueryAsync(profile, sql, State.Settings.MaxQueryRows);
                _grid.LoadResult(result);
                string suffix = result.Truncated ? " (обрезано по лимиту)" : string.Empty;
                SetStatus($"Строк: {result.Rows.Count} за {result.Elapsed.TotalMilliseconds:F0} мс{suffix}.", OpencodeTheme.Success);
                State.Log(LogLevel.Info, $"SQL: {result.Rows.Count} строк за {result.Elapsed.TotalMilliseconds:F0} мс.");
            }
            else
            {
                int affected = await provider.ExecuteNonQueryAsync(profile, sql);
                _grid.Columns.Clear();
                _grid.Rows.Clear();
                SetStatus($"Затронуто строк: {affected}.", OpencodeTheme.Success);
                State.Log(LogLevel.Info, $"SQL: затронуто строк — {affected}.");
            }
        }
        catch (Exception ex)
        {
            SetStatus("Ошибка: " + ex.Message, OpencodeTheme.Error);
            State.Log(LogLevel.Error, $"Ошибка SQL: {ex.Message}");
        }
    }

    private static bool ReturnsRows(string sql)
    {
        string trimmed = sql.TrimStart();
        foreach (var keyword in RowKeywords)
        {
            if (trimmed.StartsWith(keyword, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private void Remember(string sql)
    {
        _statements.RemoveAll(s => string.Equals(s, sql, StringComparison.Ordinal));
        _statements.Insert(0, sql);
        if (_statements.Count > 25)
        {
            _statements.RemoveRange(25, _statements.Count - 25);
        }

        _history.SetItems(_statements.Select(Preview));
    }

    private static string Preview(string sql)
    {
        string single = sql.Replace('\n', ' ').Replace('\r', ' ').Trim();
        return single.Length > 70 ? single[..70] + "…" : single;
    }

    private void SetStatus(string text, Color color)
    {
        _status.Text = text;
        _status.GlowColor = color;
    }
}
