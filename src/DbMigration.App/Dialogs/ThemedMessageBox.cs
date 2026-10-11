using System.Drawing;
using System.Windows.Forms;

namespace DbMigration.App.Dialogs;

public static class ThemedMessageBox
{
    public static void Info(IWin32Window? owner, string title, string message) =>
        Show(owner, title, message, OpencodeTheme.Info, "OK", null);

    public static void Success(IWin32Window? owner, string title, string message) =>
        Show(owner, title, message, OpencodeTheme.Success, "OK", null);

    public static void Warning(IWin32Window? owner, string title, string message) =>
        Show(owner, title, message, OpencodeTheme.Warning, "OK", null);

    public static void Error(IWin32Window? owner, string title, string message) =>
        Show(owner, title, message, OpencodeTheme.Error, "OK", null);

    public static bool Confirm(IWin32Window? owner, string title, string message, bool destructive = false)
    {
        return Show(
            owner,
            title,
            message,
            destructive ? OpencodeTheme.Error : OpencodeTheme.Warning,
            destructive ? "Да, продолжить" : "Подтвердить",
            "Отмена") == DialogResult.OK;
    }

    private static DialogResult Show(IWin32Window? owner, string title, string message, Color accent, string acceptText, string? cancelText)
    {
        using var dialog = new ThemedDialog
        {
            Text = title,
            TitleText = title,
            Size = new Size(560, 320),
            MinimumSize = new Size(420, 240),
        };

        var body = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = OpencodeTheme.Background,
            Padding = new Padding(24, 20, 24, 20),
        };

        var stripe = new Panel
        {
            Dock = DockStyle.Left,
            Width = 4,
            BackColor = accent,
        };

        var text = new Label
        {
            Dock = DockStyle.Fill,
            ForeColor = OpencodeTheme.Text,
            Font = UiFonts.Body,
            Text = message,
            AutoSize = false,
            Padding = new Padding(16, 0, 0, 0),
        };

        body.Controls.Add(text);
        body.Controls.Add(stripe);

        dialog.ContentPanel.Controls.Add(body);

        var accept = ThemedDialog.PrimaryButton(acceptText);
        if (cancelText is null)
        {
            accept.DialogResult = DialogResult.OK;
            dialog.ContentPanel.Controls.Add(ThemedDialog.CreateFooter(accept));
            dialog.AcceptButton = accept;
        }
        else
        {
            var cancel = ThemedDialog.GhostButton(cancelText);
            dialog.ContentPanel.Controls.Add(ThemedDialog.CreateFooter(accept, cancel));
            dialog.AcceptButton = accept;
            dialog.CancelButton = cancel;
        }

        return owner is null ? dialog.ShowDialog() : dialog.ShowDialog(owner);
    }
}
