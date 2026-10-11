using System.Drawing;
using System.Windows.Forms;

namespace DbMigration.App.Views;

internal static class ViewUi
{
    public static ThemedButton Button(string text, ButtonVariant variant = ButtonVariant.Default, int width = 158) => new()
    {
        Text = text,
        Variant = variant,
        Width = width,
        Height = 32,
        Margin = new Padding(0, 0, 8, 0),
    };

    public static Label Label(string text, Color? color = null, Font? font = null) => new()
    {
        Text = text,
        AutoSize = true,
        ForeColor = color ?? OpencodeTheme.TextMuted,
        Font = font ?? UiFonts.BodySmall,
        BackColor = Color.Transparent,
        Margin = new Padding(0, 0, 8, 6),
    };

    public static ThemedTextBox TextBox(string placeholder = "", bool mono = false) => new()
    {
        Placeholder = placeholder,
        UseMonoFont = mono,
        Height = 34,
    };

    public static SectionHeader Header(string title, string description = "") => new()
    {
        Title = title,
        Description = description,
        Dock = DockStyle.Top,
    };

    public static Card Card(string caption = "", int padding = 16) => new()
    {
        Caption = caption,
        Padding = new Padding(padding, caption.Length > 0 ? 42 : padding, padding, padding),
        Dock = DockStyle.Fill,
    };

    public static FlowLayoutPanel Toolbar(params Control[] controls)
    {
        var panel = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            WrapContents = false,
            BackColor = Color.Transparent,
            Padding = new Padding(0, 4, 0, 8),
        };

        foreach (var control in controls)
        {
            panel.Controls.Add(control);
        }

        return panel;
    }

    public static string EngineName(DatabaseEngine engine) => engine switch
    {
        DatabaseEngine.Sqlite => "SQLite",
        DatabaseEngine.MySql => "MySQL",
        DatabaseEngine.PostgreSql => "PostgreSQL",
        DatabaseEngine.SqlServer => "SQL Server",
        _ => engine.ToString(),
    };

    public static string ScopeName(MigrationScope scope) => scope switch
    {
        MigrationScope.Schema => "Только схема",
        MigrationScope.Data => "Только данные",
        MigrationScope.SchemaAndData => "Схема и данные",
        _ => "Ничего",
    };

    public static string ExistingName(ExistingTableBehavior behavior) => behavior switch
    {
        ExistingTableBehavior.Fail => "Ошибка",
        ExistingTableBehavior.Skip => "Пропустить",
        ExistingTableBehavior.DropAndRecreate => "Удалить и пересоздать",
        ExistingTableBehavior.TruncateAndAppend => "Очистить и дополнить",
        _ => behavior.ToString(),
    };

    public static string LevelName(LogLevel level) => level switch
    {
        LogLevel.Trace => "Трассировка",
        LogLevel.Info => "Информация",
        LogLevel.Success => "Успех",
        LogLevel.Warning => "Предупреждение",
        LogLevel.Error => "Ошибка",
        _ => level.ToString(),
    };
}
