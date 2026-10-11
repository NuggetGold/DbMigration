using DbMigration.App.Forms;

namespace DbMigration.App;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
        Application.ThreadException += (_, e) =>
            ReportError("DbMigration — ошибка", "Во время работы произошла ошибка.", e.Exception);

        try
        {
            var services = CoreServices.CreateDefault();
            var state = new AppState(services);
            state.LoadProfilesAsync().GetAwaiter().GetResult();

            var clock = AnimationClock.Instance;
            clock.Fps = services.Settings.Current.AnimationFps;
            if (services.Settings.Current.AnimationsEnabled)
            {
                clock.Start();
            }

            Application.Run(new MainForm(state));
        }
        catch (Exception ex)
        {
            ReportError("DbMigration — ошибка запуска", "DbMigration не смог запуститься.", ex);
        }
    }

    private static void ReportError(string title, string message, Exception? ex)
    {
        try
        {
            if (ex is not null)
            {
                File.WriteAllText(
                    Path.Combine(Path.GetTempPath(), "DbMigration-crash.log"),
                    DateTime.Now.ToString("O") + Environment.NewLine + ex);
            }
        }
        catch
        {
        }

        try
        {
            MessageBox.Show(
                string.IsNullOrEmpty(ex?.Message) ? message : $"{message}\n\n{ex!.Message}",
                title,
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
        catch
        {
        }
    }
}
