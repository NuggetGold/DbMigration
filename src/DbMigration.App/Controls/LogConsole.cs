using System.Drawing;
using System.Windows.Forms;

namespace DbMigration.App.Controls;

public sealed class LogConsole : RichTextBox
{
    private const int MaxBufferLength = 400_000;

    public LogConsole()
    {
        ReadOnly = true;
        BorderStyle = BorderStyle.None;
        BackColor = OpencodeTheme.BackgroundPanel;
        ForeColor = OpencodeTheme.Text;
        Font = UiFonts.Mono;
        WordWrap = false;
        HideSelection = false;
        DetectUrls = false;
        ScrollBars = RichTextBoxScrollBars.Both;
        Dock = DockStyle.Fill;
    }

    public void AppendEntry(LogEntry entry)
    {
        AppendSegment($"[{entry.Timestamp:HH:mm:ss}] ", OpencodeTheme.TextMuted);
        AppendSegment(entry.Level.ToString().ToUpperInvariant().PadRight(7), OpencodeTheme.ForLogLevel(entry.Level));
        AppendSegment(" " + entry.Message + Environment.NewLine, OpencodeTheme.Text);
        TrimIfNeeded();
        ScrollToCaret();
    }

    public void AppendSegment(string text, Color color)
    {
        SelectionStart = TextLength;
        SelectionLength = 0;
        SelectionColor = color;
        AppendText(text);
        SelectionColor = OpencodeTheme.Text;
    }

    private void TrimIfNeeded()
    {
        if (TextLength <= MaxBufferLength)
        {
            return;
        }

        int remove = TextLength - MaxBufferLength;
        Select(0, remove);
        SelectedText = string.Empty;
    }
}
