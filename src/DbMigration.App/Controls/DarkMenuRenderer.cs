using System.Drawing;
using System.Windows.Forms;

namespace DbMigration.App.Controls;

internal sealed class DarkColorTable : ProfessionalColorTable
{
    public override Color ToolStripDropDownBackground => OpencodeTheme.BackgroundPanel;

    public override Color ImageMarginGradientBegin => OpencodeTheme.BackgroundPanel;

    public override Color ImageMarginGradientMiddle => OpencodeTheme.BackgroundPanel;

    public override Color ImageMarginGradientEnd => OpencodeTheme.BackgroundPanel;

    public override Color MenuBorder => OpencodeTheme.Border;

    public override Color MenuItemBorder => OpencodeTheme.Border;

    public override Color MenuItemSelected => OpencodeTheme.BackgroundElementHover;

    public override Color MenuItemSelectedGradientBegin => OpencodeTheme.BackgroundElementHover;

    public override Color MenuItemSelectedGradientEnd => OpencodeTheme.BackgroundElementHover;

    public override Color MenuItemPressedGradientBegin => OpencodeTheme.BackgroundElementActive;

    public override Color MenuItemPressedGradientEnd => OpencodeTheme.BackgroundElementActive;

    public override Color SeparatorDark => OpencodeTheme.BorderSubtle;

    public override Color SeparatorLight => OpencodeTheme.BorderSubtle;
}

internal sealed class DarkMenuRenderer : ToolStripProfessionalRenderer
{
    public DarkMenuRenderer()
        : base(new DarkColorTable())
    {
        RoundedEdges = false;
    }

    protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
    {
        e.TextColor = e.Item is { Enabled: false } ? OpencodeTheme.TextMuted : OpencodeTheme.Text;
        base.OnRenderItemText(e);
    }

    protected override void OnRenderArrow(ToolStripArrowRenderEventArgs e)
    {
        e.ArrowColor = OpencodeTheme.TextMuted;
        base.OnRenderArrow(e);
    }
}
