namespace DbMigration.Core.Models;

public sealed class AppSettings
{
    public bool AnimationsEnabled { get; set; } = true;

    public int AnimationFps { get; set; } = 60;

    public int PageSize { get; set; } = 200;

    public int MaxQueryRows { get; set; } = 5000;

    public int MigrationBatchSize { get; set; } = 500;

    public string BackupFolder { get; set; } = string.Empty;

    public string CloudEndpoint { get; set; } = "https://cloud.example.invalid/dbmigration";

    public AppSettings Clone() => (AppSettings)MemberwiseClone();
}

