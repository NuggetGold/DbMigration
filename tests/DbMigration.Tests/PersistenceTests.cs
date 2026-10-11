using DbMigration.Core.Models;
using DbMigration.Core.Persistence;
using DbMigration.Core.Security;

namespace DbMigration.Tests;

[TestClass]
public sealed class PersistenceTests
{
    private string _directory = null!;

    [TestInitialize]
    public void Setup()
    {
        _directory = Path.Combine(Path.GetTempPath(), "DbMigrationTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_directory);
    }

    [TestCleanup]
    public void Cleanup()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    [TestMethod]
    public void SettingsStore_RoundTripsValues()
    {
        var store = new FileSettingsStore(_directory);
        store.Current.PageSize = 321;
        store.Current.CloudEndpoint = "https://example.test/upload";
        store.Save();

        var reloaded = new FileSettingsStore(_directory);
        Assert.AreEqual(321, reloaded.Current.PageSize);
        Assert.AreEqual("https://example.test/upload", reloaded.Current.CloudEndpoint);
        Assert.IsTrue(File.Exists(reloaded.StorePath));
    }

    [TestMethod]
    public void SettingsStore_UsesDefaults_WhenFileMissing()
    {
        var store = new FileSettingsStore(_directory);
        Assert.AreEqual(200, store.Current.PageSize);
        Assert.AreEqual(5000, store.Current.MaxQueryRows);
    }

    [TestMethod]
    public async Task ProfileStore_HidesPassword_OnDisk()
    {
        if (!OperatingSystem.IsWindows())
        {
            Assert.Inconclusive("Защита пароля DPAPI доступна только в Windows.");
            return;
        }

        var store = new FileProfileStore(_directory, new DpapiSecretProtector());
        var profile = new ConnectionProfile
        {
            Name = "local",
            Engine = DatabaseEngine.Sqlite,
            FilePath = "C:\\data\\app.db",
            Password = "s3cret",
        };

        await store.SaveAsync(new[] { profile });

        var json = await File.ReadAllTextAsync(store.StorePath);
        Assert.IsFalse(json.Contains("s3cret", StringComparison.Ordinal));
        Assert.IsTrue(json.Contains("ProtectedPassword", StringComparison.Ordinal));
    }

    [TestMethod]
    public async Task ProfileStore_RoundTripsPassword()
    {
        if (!OperatingSystem.IsWindows())
        {
            Assert.Inconclusive("Защита пароля DPAPI доступна только в Windows.");
            return;
        }

        var store = new FileProfileStore(_directory, new DpapiSecretProtector());
        await store.SaveAsync(new[]
        {
            new ConnectionProfile
            {
                Name = "prod",
                Engine = DatabaseEngine.PostgreSql,
                Database = "shop",
                Password = "p@ss",
            },
        });

        var loaded = await store.LoadAsync();
        Assert.HasCount(1, loaded);
        Assert.AreEqual("prod", loaded[0].Name);
        Assert.AreEqual("p@ss", loaded[0].Password);
    }

    [TestMethod]
    public async Task ProfileStore_MissingFile_ReturnsEmpty()
    {
        var store = new FileProfileStore(_directory, new DpapiSecretProtector());
        var loaded = await store.LoadAsync();
        Assert.IsEmpty(loaded);
    }

    [TestMethod]
    public void DpapiSecretProtector_RoundTripsPlaintext()
    {
        if (!OperatingSystem.IsWindows())
        {
            Assert.Inconclusive("Защита пароля DPAPI доступна только в Windows.");
            return;
        }

        var protector = new DpapiSecretProtector();
        var protectedBlob = protector.Protect("top-secret");

        Assert.IsNotNull(protectedBlob);
        Assert.AreNotEqual("top-secret", protectedBlob);
        Assert.AreEqual("top-secret", protector.Unprotect(protectedBlob));
    }

    [TestMethod]
    public void DpapiSecretProtector_HandlesEmptyInput()
    {
        var protector = new DpapiSecretProtector();
        Assert.IsNull(protector.Protect(null));
        Assert.IsNull(protector.Protect(string.Empty));
        Assert.IsNull(protector.Unprotect(null));
        Assert.IsNull(protector.Unprotect("not-base64!!"));
    }
}
