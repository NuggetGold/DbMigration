using DbMigration.Core.Cloud;
using DbMigration.Core.Export;
using DbMigration.Core.Migration;
using DbMigration.Core.Persistence;
using DbMigration.Core.Providers;
using DbMigration.Core.Security;

namespace DbMigration.Core;

public sealed record DbMigrationServices(
    IDatabaseProviderFactory Providers,
    IMigrationEngine Migration,
    IDataExporter Exporter,
    ICloudStorage Cloud,
    IProfileStore Profiles,
    ISettingsStore Settings,
    ISecretProtector Secrets);

public static class CoreServices
{
    public static DbMigrationServices CreateDefault(string? dataDirectory = null)
    {
        var directory = string.IsNullOrWhiteSpace(dataDirectory)
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "DbMigration")
            : dataDirectory;
        Directory.CreateDirectory(directory);

        var secrets = new DpapiSecretProtector();
        var providers = new DatabaseProviderFactory();
        var profiles = new FileProfileStore(directory, secrets);
        var settings = new FileSettingsStore(directory);
        var migration = new MigrationEngine(providers);
        var exporter = new DataExporter(providers);
        var cloud = new StubCloudStorage(settings.Current.CloudEndpoint);

        return new DbMigrationServices(
            providers,
            migration,
            exporter,
            cloud,
            profiles,
            settings,
            secrets);
    }
}

