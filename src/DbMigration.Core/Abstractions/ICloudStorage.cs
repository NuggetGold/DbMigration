namespace DbMigration.Core.Abstractions;

public interface ICloudStorage
{
    string Endpoint { get; }

    Task<CloudUploadResult> UploadBackupAsync(
        CloudBackupRequest request,
        IProgress<double>? progress = null,
        CancellationToken cancellationToken = default);
}

