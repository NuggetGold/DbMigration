using System.Drawing;
using System.Windows.Forms;
using DbMigration.App.Search;

namespace DbMigration.App.Dialogs;

public sealed record PaletteItem(string Title, string Category, Action Invoke);

public sealed class CommandPalette : ThemedDialog
{
    private readonly IReadOnlyList<PaletteItem> _items;
    private readonly ThemedTextBox _filter = new();
    private readonly ThemedListBox _list = new();
    private List<PaletteItem> _visible = new();

    public CommandPalette(IReadOnlyList<PaletteItem> items)
    {
        _items = items;
        TitleText = "Командная палитра";
        Size = new Size(660, 520);

        var body = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = OpencodeTheme.Background,
            Padding = new Padding(20, 16, 20, 12),
        };

        _filter.Placeholder = "Начните вводить команду…";
        _filter.Dock = DockStyle.Top;
        _filter.Height = 40;
        _filter.EditorKeyDown += OnFilterKey;
        _filter.TextChanged += (_, _) => RefreshResults();

        _list.Dock = DockStyle.Fill;
        _list.DoubleClick += (_, _) => Invoke();
        _list.KeyDown += OnListKey;

        body.Controls.Add(_list);
        body.Controls.Add(_filter);
        ContentPanel.Controls.Add(body);

        RefreshResults();
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        _filter.Focus();
    }

    private void RefreshResults()
    {
        string query = _filter.Text ?? string.Empty;
        _visible = _items
            .Select(item => (item, score: FuzzyMatcher.Score(query, item.Title + " " + item.Category)))
            .Where(pair => pair.score >= 0)
            .OrderByDescending(pair => pair.score)
            .Select(pair => pair.item)
            .ToList();

        _list.BeginUpdate();
        _list.Items.Clear();
        foreach (PaletteItem item in _visible)
        {
            _list.Items.Add($"{item.Category}  ›  {item.Title}");
        }

        _list.EndUpdate();

        if (_list.Items.Count > 0)
        {
            _list.SelectedIndex = 0;
        }
    }

    private void OnFilterKey(object? sender, KeyEventArgs e)
    {
        switch (e.KeyCode)
        {
            case Keys.Enter:
                Invoke();
                e.Handled = true;
                break;
            case Keys.Down:
                MoveSelection(1);
                e.Handled = true;
                break;
            case Keys.Up:
                MoveSelection(-1);
                e.Handled = true;
                break;
            case Keys.Escape:
                DialogResult = DialogResult.Cancel;
                Close();
                e.Handled = true;
                break;
        }
    }

    private void OnListKey(object? sender, KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Enter)
        {
            Invoke();
            e.Handled = true;
        }
    }

    private void MoveSelection(int delta)
    {
        if (_list.Items.Count == 0)
        {
            return;
        }

        int index = Math.Clamp(_list.SelectedIndex + delta, 0, _list.Items.Count - 1);
        _list.SelectedIndex = index;
    }

    private void Invoke()
    {
        int index = _list.SelectedIndex;
        if (index < 0 || index >= _visible.Count)
        {
            return;
        }

        PaletteItem item = _visible[index];
        DialogResult = DialogResult.OK;
        Close();
        item.Invoke();
    }
}
