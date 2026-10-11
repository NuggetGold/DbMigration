using DbMigration.Core.Cloud;
using DbMigration.Core.Models;

namespace DbMigration.Tests;

[TestClass]
public sealed class CloudStorageTests
{
    private sealed class CollectingProgress : IProgress<double>
    {
        public List<double> Values { get; } = new();

        public void Report(double value) => Values.Add(value);
    }

    private static CloudBackupRequest CreateRequest(int tables = 2)
    {
        return new CloudBackupRequest
        {
            Profile = new ConnectionProfile { Name = "p", Engine = DatabaseEngine.Sqlite, FilePath = "x.db" },
            Tables = Enumerable.Range(1, tables).Select(i => "t" + i).ToList(),
            IncludeData = true,
        };
    }

    [TestMethod]
    public void Constructor_FallsBackToDefaultEndpoint()
    {
        var storage = new StubCloudStorage("   ");
        StringAssert.StartsWith(storage.Endpoint, "https://");
    }

    [TestMethod]
    public void Constructor_UsesProvidedEndpoint()
    {
        var storage = new StubCloudStorage("https://example.test/upload");
        Assert.AreEqual("https://example.test/upload", storage.Endpoint);
    }

    [TestMethod]
    public async Task UploadBackup_ReportsSuccessAndProgress()
    {
        var storage = new StubCloudStorage("https://example.test/upload");
        var progress = new CollectingProgress();

        var result = await storage.UploadBackupAsync(CreateRequest(), progress);

        Assert.IsTrue(result.Success);
        Assert.IsNotNull(result.RemoteUri);
        StringAssert.StartsWith(result.RemoteUri!.ToString(), "https://example.test/upload/");
        Assert.IsGreaterThanOrEqualTo(1024, result.BytesUploaded);
        StringAssert.Contains(result.Message, "example.test");
        Assert.HasCount(12, progress.Values);
        Assert.AreEqual(1.0, progress.Values[^1], 0.0001);
    }

    [TestMethod]
    public async Task UploadBackup_HonoursCancellation()
    {
        var storage = new StubCloudStorage("https://example.test/upload");
        using var source = new CancellationTokenSource();
        source.Cancel();

        var cancelled = false;
        try
        {
            await storage.UploadBackupAsync(CreateRequest(), null, source.Token);
        }
        catch (OperationCanceledException)
        {
            cancelled = true;
        }

        Assert.IsTrue(cancelled);
    }
}
