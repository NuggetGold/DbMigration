using System.Drawing;
using System.Globalization;
using System.Text;
using System.Windows.Forms;

namespace DbMigration.App.Views;

public sealed class MigrationView : ViewBase
{
    private readonly ThemedComboBox _sourceCombo = new() { Width = 230, Placeholder = "Источник" };
    private readonly ThemedComboBox _targetCombo = new() { Width = 230, Placeholder = "Цель" };
    private readonly ThemedComboBox _existingCombo = new() { Width = 230 };
    private readonly ThemedTextBox _batchSize = ViewUi.TextBox("пакет");
    private readonly ToggleSwitch _scopeSchema = new();
    private readonly ToggleSwitch _scopeData = new();
    private readonly ToggleSwitch _stopOnError = new();
    private readonly ThemedListBox _tableList = new() { Dock = DockStyle.Fill, SelectionMode = SelectionMode.MultiExtended };
    private readonly ThemedProgressBar _progress = new() { Dock = DockStyle.Top };
    private readonly GlowLabel _status = new() { Font = UiFonts.Body, AutoSize = true, Animate = false, GlowRadius = 5, MaxIntensity = 0.5f };
    private readonly ThemedListBox _reconciliation = new() { Dock = DockStyle.Fill };
    private readonly ThemedButton _run = ViewUi.Button("Запустить миграцию", ButtonVariant.Primary, 200);
    private readonly ThemedButton _dryRun = ViewUi.Button("Предпросмотр скрипта", ButtonVariant.Default, 200);
    private readonly ThemedButton _cancel = ViewUi.Button("Отменить", ButtonVariant.Danger, 130);
    private readonly ThemedButton _reload = ViewUi.Button("Обновить таблицы", ButtonVariant.Default, 180);
    private readonly ThemedButton _selectAll = ViewUi.Button("Выбрать все", ButtonVariant.Ghost, 140);
    private readonly ThemedButton _clearSel = ViewUi.Button("Снять", ButtonVariant.Ghost, 110);
    private readonly ThemedButton _report = ViewUi.Button("Экспорт отчёта…", ButtonVariant.Ghost, 180);
    private static readonly ExistingTableBehavior[] Behaviors =
    {
        ExistingTableBehavior.Skip,
        ExistingTableBehavior.DropAndRecreate,
        ExistingTableBehavior.TruncateAndAppend,
        ExistingTableBehavior.Fail,
    };

    private List<ConnectionProfile> _profiles = new();
    private CancellationTokenSource? _cts;
    private MigrationResult? _lastResult;

    public MigrationView(AppState state)
        : base(state)
    {
        _batchSize.Width = 120;
        _batchSize.Text = State.Settings.MigrationBatchSize.ToString(CultureInfo.InvariantCulture);
        _existingCombo.SetItems(Behaviors.Select(b => (object)ViewUi.ExistingName(b)));
        _existingCombo.SelectedIndex = 0;
        _scopeSchema.Checked = true;
        _scopeData.Checked = true;
        _tableList.TextResolver = item => item?.ToString() ?? string.Empty;

        _sourceCombo.SelectedIndexChanged += (_, _) => SyncSelection();
        _targetCombo.SelectedIndexChanged += (_, _) => SyncSelection();
        _reload.Click += async (_, _) => await LoadTablesAsync();
        _selectAll.Click += (_, _) => SetAllSelected(true);
        _clearSel.Click += (_, _) => SetAllSelected(false);
        _dryRun.Click += async (_, _) => await DryRunAsync();
        _run.Click += async (_, _) => await RunAsync();
        _cancel.Click += (_, _) => _cts?.Cancel();
        _report.Click += (_, _) => ExportReport();
        _cancel.Enabled = false;
        _report.Enabled = false;

        Controls.Add(BuildBody());
        Controls.Add(ViewUi.Toolbar(_sourceCombo, _targetCombo, _reload, _dryRun, _run, _cancel));
        Controls.Add(ViewUi.Header("Миграция", "Перенос схемы и данных между базами с предпросмотром и сверкой количества строк."));

        State.ProfilesChanged += (_, _) => ReloadProfiles();
        State.SelectionChanged += (_, _) => ReloadProfiles();
        ReloadProfiles();
    }

    public override string ViewTitle => "Миграция";

    public override Task ActivateAsync()
    {
        ReloadProfiles();
        return Task.CompletedTask;
    }

    private Control BuildBody()
    {
        var left = ViewUi.Card("Параметры");
        var leftFlow = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoScroll = true,
            BackColor = Color.Transparent,
        };
        leftFlow.Controls.Add(ViewUi.Label("Что переносить", OpencodeTheme.TextMuted, UiFonts.BodySmall));
        leftFlow.Controls.Add(MakeToggleRow("Схема (структура таблиц)", _scopeSchema));
        leftFlow.Controls.Add(MakeToggleRow("Данные (строки)", _scopeData));
        leftFlow.Controls.Add(ViewUi.Label("Существующие таблицы в цели", OpencodeTheme.TextMuted, UiFonts.BodySmall));
        leftFlow.Controls.Add(_existingCombo);
        leftFlow.Controls.Add(ViewUi.Label("Размер пакета", OpencodeTheme.TextMuted, UiFonts.BodySmall));
        leftFlow.Controls.Add(_batchSize);
        leftFlow.Controls.Add(MakeToggleRow("Остановиться при первой ошибке", _stopOnError));
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

        var top = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            BackColor = OpencodeTheme.Background,
        };
        top.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 44f));
        top.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 56f));
        top.Controls.Add(left, 0, 0);
        top.Controls.Add(right, 1, 0);

        var bottom = ViewUi.Card("Ход выполнения и сверка");
        var bottomInner = new Panel { Dock = DockStyle.Fill, BackColor = Color.Transparent };
        var progressHost = new Panel { Dock = DockStyle.Top, Height = 24, Padding = new Padding(0, 6, 0, 8) };
        progressHost.Controls.Add(_progress);
        var actions = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            AutoSize = true,
            WrapContents = false,
            BackColor = Color.Transparent,
        };
        actions.Controls.Add(_report);
        actions.Controls.Add(_status);
        bottomInner.Controls.Add(_reconciliation);
        bottomInner.Controls.Add(actions);
        bottomInner.Controls.Add(progressHost);
        bottom.Controls.Add(bottomInner);

        var body = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            BackColor = OpencodeTheme.Background,
        };
        body.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
        body.RowStyles.Add(new RowStyle(SizeType.Absolute, 240f));
        body.Controls.Add(top, 0, 0);
        body.Controls.Add(bottom, 0, 1);
        return body;
    }

    private static Control MakeToggleRow(string text, ToggleSwitch toggle)
    {
        var panel = new Panel { Width = 360, Height = 34, BackColor = Color.Transparent };
        var label = new Label
        {
            Text = text,
            ForeColor = OpencodeTheme.Text,
            Font = UiFonts.Body,
            AutoSize = true,
            Location = new Point(0, 6),
        };
        toggle.SetBounds(200, 4, 48, 26);
        panel.Controls.Add(toggle);
        panel.Controls.Add(label);
        return panel;
    }

    private ConnectionProfile? SourceProfile =>
        _sourceCombo.SelectedIndex >= 0 && _sourceCombo.SelectedIndex < _profiles.Count
            ? _profiles[_sourceCombo.SelectedIndex]
            : null;

    private ConnectionProfile? TargetProfile =>
        _targetCombo.SelectedIndex >= 0 && _targetCombo.SelectedIndex < _profiles.Count
            ? _profiles[_targetCombo.SelectedIndex]
            : null;

    private void ReloadProfiles()
    {
        _profiles = State.Profiles.ToList();
        var names = _profiles.Select(p => (object)p.Name).ToList();

        int sourceIndex = IndexOf(State.SourceProfile);
        int targetIndex = IndexOf(State.TargetProfile);

        _sourceCombo.SetItems(names);
        _targetCombo.SetItems(names);
        _sourceCombo.SelectedIndex = sourceIndex >= 0 ? sourceIndex : _profiles.Count > 0 ? 0 : -1;
        _targetCombo.SelectedIndex = targetIndex >= 0 ? targetIndex : _profiles.Count > 1 ? 1 : _profiles.Count > 0 ? 0 : -1;
    }

    private int IndexOf(ConnectionProfile? profile) =>
        profile is null ? -1 : _profiles.FindIndex(p => p.Id == profile.Id);

    private void SyncSelection()
    {
        State.SetSource(SourceProfile);
        State.SetTarget(TargetProfile);
    }

    private async Task LoadTablesAsync()
    {
        _tableList.Items.Clear();
        if (SourceProfile is not { } profile)
        {
            return;
        }

        try
        {
            var names = await State.Services.Migration.GetTableNamesAsync(profile);
            foreach (var name in names)
            {
                _tableList.Items.Add(name);
            }

            if (_tableList.Items.Count > 0)
            {
                SetAllSelected(true);
                _tableList.SelectedIndex = 0;
            }
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

    private IReadOnlyList<string> SelectedTables()
    {
        var chosen = _tableList.SelectedItems.Cast<object>().Select(o => o.ToString() ?? string.Empty).Where(s => s.Length > 0).ToList();
        return chosen.Count > 0 ? chosen : _tableList.Items.Cast<object>().Select(o => o.ToString() ?? string.Empty).ToList();
    }

    private MigrationScope CurrentScope()
    {
        var scope = MigrationScope.None;
        if (_scopeSchema.Checked)
        {
            scope |= MigrationScope.Schema;
        }

        if (_scopeData.Checked)
        {
            scope |= MigrationScope.Data;
        }

        return scope;
    }

    private int CurrentBatchSize() =>
        int.TryParse(_batchSize.Text.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int size)
            ? Math.Clamp(size, 1, 100000)
            : State.Settings.MigrationBatchSize;

    private MigrationRequest? BuildRequest()
    {
        if (SourceProfile is not { } source || TargetProfile is not { } target)
        {
            ThemedMessageBox.Warning(this, "Миграция", "Выберите источник и цель.");
            return null;
        }

        MigrationScope scope = CurrentScope();
        if (scope == MigrationScope.None)
        {
            ThemedMessageBox.Warning(this, "Миграция", "Выберите, что переносить: схему и/или данные.");
            return null;
        }

        return new MigrationRequest
        {
            Source = source,
            Target = target,
            Tables = SelectedTables(),
            Scope = scope,
            ExistingTables = Behaviors[Math.Max(0, _existingCombo.SelectedIndex)],
            BatchSize = CurrentBatchSize(),
            StopOnFirstError = _stopOnError.Checked,
        };
    }

    private async Task DryRunAsync()
    {
        if (BuildRequest() is not { } request)
        {
            return;
        }

        SetStatus("Генерация скрипта…", OpencodeTheme.Info);
        try
        {
            string script = await State.Services.Migration.GenerateSqlScriptAsync(request);
            SetStatus($"Скрипт сформирован ({script.Length} символов).", OpencodeTheme.Success);
            using var dialog = new DryRunDialog("Предпросмотр SQL", script);
            dialog.ShowDialog(this);
        }
        catch (Exception ex)
        {
            SetStatus("Ошибка: " + ex.Message, OpencodeTheme.Error);
        }
    }

    private async Task RunAsync()
    {
        if (BuildRequest() is not { } request)
        {
            return;
        }

        if (!ThemedMessageBox.Confirm(
                this,
                "Миграция",
                $"Перенести {request.Tables.Count} табл. из «{request.Source.Name}» в «{request.Target.Name}»?\n\nРежим существующих таблиц: {ViewUi.ExistingName(request.ExistingTables)}.",
                destructive: request.ExistingTables is ExistingTableBehavior.DropAndRecreate or ExistingTableBehavior.TruncateAndAppend))
        {
            return;
        }

        _reconciliation.Items.Clear();
        _cts = new CancellationTokenSource();
        SetBusy(true);
        _progress.Value = 0;
        _progress.Marquee = false;

        var progress = new Progress<MigrationProgress>(p =>
        {
            if (p.RowsTotal is > 0)
            {
                _progress.Value = (int)Math.Round(p.Fraction * 100);
            }
            else
            {
                _progress.Marquee = true;
            }

            SetStatus(p.Message, OpencodeTheme.Info);
        });

        try
        {
            var result = await State.Services.Migration.MigrateAsync(request, progress, _cts.Token);
            _lastResult = result;
            _progress.Marquee = false;
            _progress.Value = result.Success ? 100 : _progress.Value;
            await ReconcileAsync(request.Source, request.Target, request.Tables);
            _report.Enabled = true;

            if (result.Success)
            {
                SetStatus($"Готово: {result.TotalRowsCopied} строк за {result.Elapsed.TotalSeconds:F1} с.", OpencodeTheme.Success);
                State.Log(LogLevel.Success, $"Миграция завершена: {result.SucceededTables}/{result.Tables.Count} таблиц, {result.TotalRowsCopied} строк.");
            }
            else
            {
                SetStatus($"Завершено с ошибками: {result.FatalError ?? "см. отчёт"}.", OpencodeTheme.Error);
                State.Log(LogLevel.Error, $"Миграция завершена с ошибками: {result.FatalError ?? "подробности в отчёте"}");
            }
        }
        catch (OperationCanceledException)
        {
            SetStatus("Миграция отменена.", OpencodeTheme.Warning);
            State.Log(LogLevel.Warning, "Миграция отменена пользователем.");
        }
        catch (Exception ex)
        {
            SetStatus("Ошибка: " + ex.Message, OpencodeTheme.Error);
            State.Log(LogLevel.Error, $"Ошибка миграции: {ex.Message}");
        }
        finally
        {
            SetBusy(false);
            _cts?.Dispose();
            _cts = null;
        }
    }

    private async Task ReconcileAsync(ConnectionProfile source, ConnectionProfile target, IReadOnlyList<string> tables)
    {
        var sourceProvider = State.Services.Providers.Get(source.Engine);
        var targetProvider = State.Services.Providers.Get(target.Engine);

        foreach (var table in tables)
        {
            try
            {
                long? before = await sourceProvider.GetRowCountAsync(source, table);
                long? after = await targetProvider.GetRowCountAsync(target, table);
                string line = $"{table}:  было {before?.ToString() ?? "?"}  →  стало {after?.ToString() ?? "?"}";
                if (before is not null && after is not null && before != after)
                {
                    line += "   ● расхождение";
                }

                _reconciliation.Items.Add(line);
            }
            catch (Exception ex)
            {
                _reconciliation.Items.Add($"{table}:  сверка недоступна ({ex.Message})");
            }
        }
    }

    private void ExportReport()
    {
        if (_lastResult is not { } result)
        {
            return;
        }

        using var dialog = new SaveFileDialog
        {
            Title = "Сохранить отчёт о миграции",
            Filter = "Markdown (*.md)|*.md|JSON (*.json)|*.json|Все файлы (*.*)|*.*",
            FileName = $"migration-{DateTime.Now:yyyyMMdd-HHmmss}.md",
        };

        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        try
        {
            string text = dialog.FilterIndex == 2 ? BuildJsonReport(result) : BuildMarkdownReport(result);
            File.WriteAllText(dialog.FileName, text, new UTF8Encoding(false));
            State.Log(LogLevel.Success, $"Отчёт сохранён: {dialog.FileName}.");
            ThemedMessageBox.Success(this, "Отчёт", $"Отчёт сохранён:\n{dialog.FileName}");
        }
        catch (Exception ex)
        {
            State.Log(LogLevel.Error, $"Не удалось сохранить отчёт: {ex.Message}");
            ThemedMessageBox.Error(this, "Отчёт", ex.Message);
        }
    }

    private string BuildMarkdownReport(MigrationResult result)
    {
        var builder = new StringBuilder();
        builder.AppendLine("# Отчёт о миграции DbMigration");
        builder.AppendLine();
        builder.AppendLine($"- Дата: {DateTime.Now:g}");
        builder.AppendLine($"- Источник: {SourceProfile?.Name}");
        builder.AppendLine($"- Цель: {TargetProfile?.Name}");
        builder.AppendLine($"- Успех: {(result.Success ? "да" : "нет")}");
        builder.AppendLine($"- Строк перенесено: {result.TotalRowsCopied}");
        builder.AppendLine($"- Время: {result.Elapsed.TotalSeconds:F1} с");
        builder.AppendLine();
        builder.AppendLine("| Таблица | Статус | Строк | Время (с) | Ошибка |");
        builder.AppendLine("|---|---|---:|---:|---|");
        foreach (var table in result.Tables)
        {
            builder.AppendLine($"| {table.Table} | {(table.Success ? "OK" : "Ошибка")} | {table.RowsCopied} | {table.Elapsed.TotalSeconds:F1} | {table.Error ?? string.Empty} |");
        }

        return builder.ToString();
    }

    private static string BuildJsonReport(MigrationResult result)
    {
        var payload = new
        {
            generatedAt = DateTime.Now,
            success = result.Success,
            totalRowsCopied = result.TotalRowsCopied,
            elapsedSeconds = Math.Round(result.Elapsed.TotalSeconds, 2),
            fatalError = result.FatalError,
            tables = result.Tables.Select(t => new
            {
                table = t.Table,
                success = t.Success,
                rowsCopied = t.RowsCopied,
                elapsedSeconds = Math.Round(t.Elapsed.TotalSeconds, 2),
                error = t.Error,
            }),
        };
        return System.Text.Json.JsonSerializer.Serialize(payload, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
    }

    private void SetStatus(string text, Color color)
    {
        _status.Text = text;
        _status.GlowColor = color;
    }

    private void SetBusy(bool busy)
    {
        _run.Enabled = !busy;
        _dryRun.Enabled = !busy;
        _cancel.Enabled = busy;
        _reload.Enabled = !busy;
        if (busy)
        {
            SetStatus("Выполняется…", OpencodeTheme.Info);
        }
    }
}
