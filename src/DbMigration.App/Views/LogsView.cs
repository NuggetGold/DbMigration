using System.Drawing;
using System.Text;
using System.Windows.Forms;

namespace DbMigration.App.Views;

public sealed class LogsView : ViewBase
{
    private readonly ThemedComboBox _filterCombo = new() { Width = 220 };
    private readonly LogConsole _console = new();
    private readonly ThemedButton _clear = ViewUi.Button("Очистить", ButtonVariant.Danger, 140);
    private readonly ThemedButton _export = ViewUi.Button("Экспорт журнала…", ButtonVariant.Default, 200);

    public LogsView(AppState state)
        : base(state)
    {
        _filterCombo.SetItems(new object[] { "Все" }.Concat(Enum.GetValues<LogLevel>().Select(l => (object)ViewUi.LevelName(l))));
        _filterCombo.SelectedIndex = 0;
        _filterCombo.SelectedIndexChanged += (_, _) => Rebuild();

        _clear.Click += (_, _) =>
        {
            State.Activity.Clear();
            _console.Clear();
        };
        _export.Click += (_, _) => ExportLog();

        var host = ViewUi.Card();
        host.Controls.Add(_console);

        Controls.Add(host);
        Controls.Add(ViewUi.Toolbar(_filterCombo, _clear, _export));
        Controls.Add(ViewUi.Header("Журнал", "Все действия приложения: подключения, миграции, экспорт и ошибки."));

        State.Activity.EntryAdded += OnEntryAdded;
        Rebuild();
    }

    public override string ViewTitle => "Журнал";

    public override Task ActivateAsync()
    {
        Rebuild();
        return Task.CompletedTask;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            State.Activity.EntryAdded -= OnEntryAdded;
        }

        base.Dispose(disposing);
    }

    private void OnEntryAdded(object? sender, LogEntry entry)
    {
        if (IsDisposed || !IsHandleCreated)
        {
            return;
        }

        if (InvokeRequired)
        {
            BeginInvoke(new Action(() => AppendIfVisible(entry)));
        }
        else
        {
            AppendIfVisible(entry);
        }
    }

    private void AppendIfVisible(LogEntry entry)
    {
        if (Matches(entry))
        {
            _console.AppendEntry(entry);
        }
    }

    private bool Matches(LogEntry entry)
    {
        if (_filterCombo.SelectedIndex <= 0)
        {
            return true;
        }

        var levels = Enum.GetValues<LogLevel>();
        int index = _filterCombo.SelectedIndex - 1;
        return index < levels.Length && levels[index] == entry.Level;
    }

    private void Rebuild()
    {
        _console.Clear();
        foreach (var entry in State.Activity.Snapshot())
        {
            if (Matches(entry))
            {
                _console.AppendEntry(entry);
            }
        }
    }

    private void ExportLog()
    {
        using var dialog = new SaveFileDialog
        {
            Title = "Сохранить журнал",
            Filter = "Текстовый файл (*.txt)|*.txt|Все файлы (*.*)|*.*",
            FileName = $"dbmigration-log-{DateTime.Now:yyyyMMdd-HHmmss}.txt",
        };

        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        try
        {
            File.WriteAllText(dialog.FileName, State.Activity.ToText(), new UTF8Encoding(false));
            State.Log(LogLevel.Success, $"Журнал сохранён: {dialog.FileName}.");
            ThemedMessageBox.Success(this, "Журнал", $"Журнал сохранён:\n{dialog.FileName}");
        }
        catch (Exception ex)
        {
            ThemedMessageBox.Error(this, "Журнал", ex.Message);
        }
    }
}
