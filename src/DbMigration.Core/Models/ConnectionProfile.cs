using System.Text.Json.Serialization;

namespace DbMigration.Core.Models;

public sealed class ConnectionProfile
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string Name { get; set; } = string.Empty;

    public DatabaseEngine Engine { get; set; } = DatabaseEngine.Sqlite;

    public string? FilePath { get; set; }

    public string? Host { get; set; } = "localhost";

    public int? Port { get; set; }

    public string? Database { get; set; }

    public string? User { get; set; }

    [JsonIgnore]
    public string? Password { get; set; }

    public string? ProtectedPassword { get; set; }

    public bool IntegratedSecurity { get; set; }

    public bool TrustServerCertificate { get; set; } = true;

    public string? ExtraOptions { get; set; }

    public DateTimeOffset LastUsedUtc { get; set; } = DateTimeOffset.UtcNow;

    public ConnectionProfile Clone() => new()
    {
        Id = Id,
        Name = Name,
        Engine = Engine,
        FilePath = FilePath,
        Host = Host,
        Port = Port,
        Database = Database,
        User = User,
        Password = Password,
        ProtectedPassword = ProtectedPassword,
        IntegratedSecurity = IntegratedSecurity,
        TrustServerCertificate = TrustServerCertificate,
        ExtraOptions = ExtraOptions,
        LastUsedUtc = LastUsedUtc,
    };

    [JsonIgnore]
    public string DisplayTarget => Engine == DatabaseEngine.Sqlite
        ? FilePath ?? "(файл не выбран)"
        : $"{Host}{(Port is > 0 ? ":" + Port : string.Empty)}/{Database}";
}

