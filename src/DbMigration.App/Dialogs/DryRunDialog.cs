using System.Drawing;
using System.Windows.Forms;

namespace DbMigration.App.Dialogs;

public sealed class DryRunDialog : ThemedDialog
{
    public DryRunDialog(string title, string script)
    {
        TitleText = title;
        Size = new Size(900, 660);

        var body = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = OpencodeTheme.Background,
            Padding = new Padding(20, 16, 20, 16),
        };

        var box = new ThemedTextBox
        {
            Dock = DockStyle.Fill,
            Multiline = true,
            ReadOnly = true,
            UseMonoFont = true,
            Text = script,
        };

        var copy = new ThemedButton { Text = "Копировать", Variant = ButtonVariant.Default, Width = 150, Height = 34 };
        copy.Click += (_, _) =>
        {
            if (box.Text.Length > 0)
            {
                Clipboard.SetText(box.Text);
                ThemedMessageBox.Success(this, "Готово", "Скрипт скопирован в буфер обмена.");
            }
        };

        var close = GhostButton("Закрыть");

        body.Controls.Add(box);
        ContentPanel.Controls.Add(body);
        ContentPanel.Controls.Add(CreateFooter(copy, close));
        AcceptButton = close;
        CancelButton = close;
    }
}
