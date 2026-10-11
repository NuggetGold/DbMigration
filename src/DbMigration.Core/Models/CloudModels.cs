namespace DbMigration.Core.Models;

public sealed record CloudBackupRequest
{
    public required ConnectionProfile Profile { get; init; }

    public IReadOnlyList<string> Tables { get; init; } = Array.Empty<string>();

    public bool IncludeData { get; init; } = true;

    public string? LocalArchivePath { get; init; }
}

public sealed record CloudUploadResult(
    bool Success,
    string Message,
    Uri? RemoteUri,
    long BytesUploaded,
    DateTimeOffset CompletedAtUtc);

