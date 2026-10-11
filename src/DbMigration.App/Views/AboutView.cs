using System.Drawing;
using System.Windows.Forms;

namespace DbMigration.App.Views;

public sealed class AboutView : ViewBase
{
    public AboutView(AppState state)
        : base(state)
    {
        var flow = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoScroll = true,
            BackColor = Color.Transparent,
            Padding = new Padding(80, 36, 80, 24),
        };

        var title = new GlowLabel
        {
            Text = "DbMigration",
            Font = UiFonts.DisplayHuge,
            GlowColor = OpencodeTheme.Accent,
            GlowHighlight = OpencodeTheme.PurpleLight,
            ShineEnabled = true,
            Animate = true,
            AutoSize = true,
            Margin = new Padding(0, 0, 0, 4),
        };

        var tagline = new Label
        {
            Text = "Универсальная конвертация баз данных: схемы, данные, SQL",
            ForeColor = OpencodeTheme.Secondary,
            Font = UiFonts.Display,
            AutoSize = true,
            BackColor = Color.Transparent,
            Margin = new Padding(0, 0, 0, 20),
        };

        var version = ViewUi.Label($"Версия {VersionText()}", OpencodeTheme.TextMuted, UiFonts.BodySmall);
        version.Margin = new Padding(0, 0, 0, 24);

        flow.Controls.Add(title);
        flow.Controls.Add(tagline);
        flow.Controls.Add(version);
        flow.Controls.Add(BuildSection("Возможности", FeatureLines(), OpencodeTheme.Primary));
        flow.Controls.Add(BuildSection("Технологии", TechLines(), OpencodeTheme.Info));
        flow.Controls.Add(BuildFooter());

        Controls.Add(flow);
        Controls.Add(ViewUi.Header("О программе", "DbMigration — конвертация, просмотр и редактирование баз данных."));
    }

    public override string ViewTitle => "О программе";

    private Control BuildSection(string title, IReadOnlyList<string> lines, Color accent)
    {
        var panel = new Panel { Width = 640, AutoSize = true, BackColor = Color.Transparent, Margin = new Padding(0, 0, 0, 18) };
        var heading = new Label
        {
            Text = title,
            ForeColor = accent,
            Font = UiFonts.Section,
            AutoSize = true,
            BackColor = Color.Transparent,
            Location = new Point(0, 0),
        };
        panel.Controls.Add(heading);

        int y = heading.Height + 8;
        foreach (var line in lines)
        {
            var label = new Label
            {
                Text = "•  " + line,
                ForeColor = OpencodeTheme.Text,
                Font = UiFonts.Body,
                AutoSize = true,
                MaximumSize = new Size(640, 0),
                BackColor = Color.Transparent,
                Location = new Point(0, y),
                Padding = new Padding(0, 2, 0, 2),
            };
            panel.Controls.Add(label);
            y += label.Height + 2;
        }

        return panel;
    }

    private Control BuildFooter()
    {
        var panel = new Panel { Width = 640, Height = 90, BackColor = OpencodeTheme.BackgroundElement, Margin = new Padding(0, 10, 0, 0) };
        panel.Paint += (_, e) =>
        {
            using var pen = new Pen(OpencodeTheme.Border);
            e.Graphics.DrawRectangle(pen, 0, 0, panel.Width - 1, panel.Height - 1);
        };

        var authorCaption = new Label
        {
            Text = "Автор:",
            ForeColor = OpencodeTheme.TextMuted,
            Font = UiFonts.BodySmall,
            AutoSize = true,
            BackColor = Color.Transparent,
            Location = new Point(16, 14),
        };

        var authorName = new Label
        {
            Text = "@NuggetGold",
            ForeColor = OpencodeTheme.SkyBlue,
            Font = UiFonts.BodyBold,
            AutoSize = true,
            BackColor = Color.Transparent,
            Location = new Point(16 + TextRenderer.MeasureText("Автор:", UiFonts.BodySmall).Width + 8, 12),
        };

        var github = new ThemedLink
        {
            Text = "github.com/NuggetGold",
            Url = "https://github.com/NuggetGold",
            Animate = true,
            PulseFrom = OpencodeTheme.PurpleDark,
            PulseTo = OpencodeTheme.PurpleLight,
            Font = UiFonts.Body,
            AutoSize = true,
            Location = new Point(16, 52),
        };

        var hint = new Label
        {
            Text = "Проект разработан в рамках курс-практики · некоммерческий",
            ForeColor = OpencodeTheme.TextMuted,
            Font = UiFonts.Caption,
            AutoSize = true,
            BackColor = Color.Transparent,
            Location = new Point(16, 76),
        };

        panel.Controls.Add(authorCaption);
        panel.Controls.Add(authorName);
        panel.Controls.Add(github);
        panel.Controls.Add(hint);
        return panel;
    }

    private static IReadOnlyList<string> FeatureLines() => new[]
    {
        "Конвертация баз данных между SQLite, MySQL, PostgreSQL и SQL Server",
        "Просмотр и постраничное редактирование данных с первичным ключом",
        "SQL-консоль с защитой от опасных операторов",
        "Предпросмотр скрипта миграции без изменения целевой базы",
        "Сверка количества строк до и после миграции",
        "Экспорт в SQL, CSV и JSON, отчёты в Markdown и JSON",
        "Резервное копирование в облако (имитация хранилища)",
        "Глобальный поиск по схеме и командная палитра (Ctrl+K)",
        "Пароли защищены средствами Windows (DPAPI)",
    };

    private static IReadOnlyList<string> TechLines() => new[]
    {
        "C# / .NET 10 · WinForms · код-фёрст интерфейс без дизайнера",
        "Microsoft.Data.Sqlite · MySqlConnector · Npgsql · Microsoft.Data.SqlClient",
        "Тёмная тема в стиле opencode с пульсирующим свечением текста",
    };

    private static string VersionText()
    {
        var version = typeof(AboutView).Assembly.GetName().Version;
        return version is null ? "1.0" : $"{version.Major}.{version.Minor}.{version.Build}";
    }
}