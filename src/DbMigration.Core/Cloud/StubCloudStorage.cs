using System.Diagnostics;

namespace DbMigration.Core.Cloud;

public sealed class StubCloudStorage : ICloudStorage
{
    private const int ProgressSteps = 12;
    private const string DefaultEndpoint = "https://cloud.example.invalid/dbmigration";
    private static readonly TimeSpan StepDelay = TimeSpan.FromMilliseconds(75);

    public StubCloudStorage(string? endpoint = null)
    {
        Endpoint = string.IsNullOrWhiteSpace(endpoint) ? DefaultEndpoint : endpoint.Trim();
    }

    public string Endpoint { get; }

    public async Task<CloudUploadResult> UploadBackupAsync(
        CloudBackupRequest request,
        IProgress<double>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var bytes = EstimateBytes(request);
        var stopwatch = Stopwatch.StartNew();

        for (var step = 1; step <= ProgressSteps; step++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await Task.Delay(StepDelay, cancellationToken).ConfigureAwait(false);
            progress?.Report((double)step / ProgressSteps);
        }

        stopwatch.Stop();

        var remoteUri = new Uri(Endpoint.TrimEnd('/') + "/" + Guid.NewGuid().ToString("N"));
        var message =
            $"Резервная копия загружена в {Endpoint} ({request.Tables.Count} табл., {bytes} байт за {stopwatch.ElapsedMilliseconds} мс).";

        return new CloudUploadResult(true, message, remoteUri, bytes, DateTimeOffset.UtcNow);
    }

    private static long EstimateBytes(CloudBackupRequest request)
    {
        if (!string.IsNullOrWhiteSpace(request.LocalArchivePath) && File.Exists(request.LocalArchivePath))
        {
            try
            {
                return new FileInfo(request.LocalArchivePath).Length;
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }

        var tableCount = request.Tables.Count > 0 ? request.Tables.Count : 1;
        return Math.Max(1024, tableCount * 4096L);
    }
}

