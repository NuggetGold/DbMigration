using System.Drawing;
using System.Drawing.Text;
using System.Windows.Forms;

namespace DbMigration.App.Controls;

public sealed class ThemedTreeView : TreeView
{
    public ThemedTreeView()
    {
        DrawMode = TreeViewDrawMode.OwnerDrawAll;
        BackColor = OpencodeTheme.BackgroundPanel;
        ForeColor = OpencodeTheme.Text;
        BorderStyle = BorderStyle.None;
        HideSelection = false;
        FullRowSelect = true;
        ShowLines = false;
        ShowPlusMinus = false;
        ShowRootLines = true;
        ItemHeight = 24;
        Indent = 16;
        Font = UiFonts.Body;
        DoubleBuffered = true;
    }

    protected override void OnDrawNode(DrawTreeNodeEventArgs e)
    {
        if (e.Node is null)
        {
            return;
        }

        var g = e.Graphics;
        g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
        var bounds = e.Bounds;
        if (bounds.Width <= 0 || bounds.Height <= 0)
        {
            return;
        }

        bool selected = (e.State & TreeNodeStates.Selected) != 0;
        Color background = selected
            ? OpencodeTheme.WithAlpha(OpencodeTheme.Accent, 110)
            : OpencodeTheme.BackgroundPanel;
        using (var brush = new SolidBrush(background))
        {
            g.FillRectangle(brush, bounds);
        }

        int textLeft = bounds.Left + 6;
        if (e.Node.Nodes.Count > 0)
        {
            var center = new PointF(bounds.Left + 10, bounds.Top + bounds.Height / 2f);
            ThemeRenderer.DrawTriangle(g, center, OpencodeTheme.TextMuted, e.Node.IsExpanded);
            textLeft = bounds.Left + 24;
        }

        Color color = e.Node.ForeColor.IsEmpty ? OpencodeTheme.Text : e.Node.ForeColor;
        var textRect = new Rectangle(textLeft, bounds.Top, Math.Max(1, bounds.Right - textLeft - 4), bounds.Height);
        TextRenderer.DrawText(
            g,
            e.Node.Text,
            Font,
            textRect,
            color,
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix | TextFormatFlags.EndEllipsis);
    }

    protected override void OnNodeMouseDoubleClick(TreeNodeMouseClickEventArgs e)
    {
        base.OnNodeMouseDoubleClick(e);
        if (e.Node is { Nodes.Count: > 0 })
        {
            e.Node.Toggle();
        }
    }
}
