using System.Drawing;
using System.Windows.Forms;

namespace DbMigration.App.Dialogs;

public class ThemedDialog : ThemedForm
{
    public ThemedDialog()
    {
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.CenterParent;
        ShowMinimizeButton = false;
        ShowMaximizeButton = false;
        KeyPreview = true;
        Size = new Size(620, 480);
        MinimumSize = new Size(420, 300);
    }

    internal static ThemedButton PrimaryButton(string text) => new()
    {
        Text = text,
        Variant = ButtonVariant.Primary,
        DialogResult = DialogResult.OK,
        Width = 140,
        Height = 34,
    };

    internal static ThemedButton GhostButton(string text) => new()
    {
        Text = text,
        Variant = ButtonVariant.Ghost,
        DialogResult = DialogResult.Cancel,
        Width = 120,
        Height = 34,
    };

    internal static Panel CreateFooter(params Control[] buttons)
    {
        var footer = new Panel
        {
            Dock = DockStyle.Bottom,
            Height = 62,
            BackColor = OpencodeTheme.BackgroundPanel,
            Padding = new Padding(16, 12, 16, 12),
        };

        var flow = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false,
            BackColor = Color.Transparent,
        };

        foreach (var button in buttons)
        {
            button.Margin = new Padding(8, 0, 0, 0);
            flow.Controls.Add(button);
        }

        footer.Controls.Add(flow);
        footer.Paint += (_, e) =>
        {
            using var pen = new Pen(OpencodeTheme.BorderSubtle);
            e.Graphics.DrawLine(pen, 0, 0, footer.Width, 0);
        };

        return footer;
    }
}
