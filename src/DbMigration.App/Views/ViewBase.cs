namespace DbMigration.App.Views;

public abstract class ViewBase : UserControl
{
    protected ViewBase(AppState state)
    {
        State = state ?? throw new ArgumentNullException(nameof(state));
        Dock = DockStyle.Fill;
        BackColor = OpencodeTheme.Background;
        ForeColor = OpencodeTheme.Text;
        Font = UiFonts.Body;
        DoubleBuffered = true;
        Padding = new Padding(20, 16, 20, 16);
    }

    protected AppState State { get; }

    public abstract string ViewTitle { get; }

    public virtual Task ActivateAsync() => Task.CompletedTask;
}
