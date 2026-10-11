using System.Drawing;
using System.Windows.Forms;

namespace DbMigration.App.Dialogs;

public sealed class CloudBackupDialog : ThemedDialog
{
    private readonly ICloudStorage _cloud;
    private readonly CloudBackupRequest _request;
    private readonly ThemedProgressBar _progress = new() { Dock = DockStyle.Top, Height = 10 };
    private readonly GlowLabel _headline;
    private readonly Label _detail;
    private readonly ThemedLink _remote;
    private readonly ThemedButton _close;

    public CloudBackupDialog(ICloudStorage cloud, CloudBackupRequest request)
    {
        _cloud = cloud;
        _request = request;
        TitleText = "Резервное копирование в облако";
        Size = new Size(620, 420);

        var body = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = OpencodeTheme.Background,
            Padding = new Padding(28, 24, 28, 24),
        };

        _headline = new GlowLabel
        {
            Text = "Загрузка…",
            Font = UiFonts.DisplayLarge,
            GlowColor = OpencodeTheme.Info,
            GlowHighlight = OpencodeTheme.PrimaryBright,
            ShineEnabled = true,
            Dock = DockStyle.Top,
            Height = 52,
        };

        _detail = new Label
        {
            Dock = DockStyle.Fill,
            ForeColor = OpencodeTheme.TextMuted,
            Font = UiFonts.Body,
            Text = $"Узел: {_cloud.Endpoint}",
            Padding = new Padding(0, 12, 0, 0),
        };

        _remote = new ThemedLink
        {
            Dock = DockStyle.Bottom,
            Height = 22,
            Visible = false,
        };

        _close = GhostButton("Закрыть");
        _close.Enabled = false;

        var progressHost = new Panel { Dock = DockStyle.Top, Height = 10, Padding = new Padding(0, 20, 0, 0) };
        progressHost.Controls.Add(_progress);

        body.Controls.Add(_detail);
        body.Controls.Add(_remote);
        body.Controls.Add(progressHost);
        body.Controls.Add(_headline);

        ContentPanel.Controls.Add(body);
        ContentPanel.Controls.Add(CreateFooter(_close));
        CancelButton = _close;
    }

    public CloudUploadResult? Result { get; private set; }

    protected override async void OnShown(EventArgs e)
    {
        base.OnShown(e);

        try
        {
            var progress = new Progress<double>(value =>
            {
                _progress.Value = (int)Math.Round(value * 100);
            });

            Result = await _cloud.UploadBackupAsync(_request, progress);
            _progress.Value = 100;
            _headline.Text = "Успешно";
            _headline.GlowColor = OpencodeTheme.Success;
            _detail.Text = Result.Message;
            if (Result.RemoteUri is { } uri)
            {
                _remote.Text = uri.ToString();
                _remote.Url = uri.ToString();
                _remote.Visible = true;
            }
        }
        catch (Exception ex)
        {
            _headline.Text = "Ошибка";
            _headline.GlowColor = OpencodeTheme.Error;
            _detail.Text = ex.Message;
        }
        finally
        {
            _close.Enabled = true;
            _close.Focus();
        }
    }
}
