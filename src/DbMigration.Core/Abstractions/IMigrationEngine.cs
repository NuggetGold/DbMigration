namespace DbMigration.Core.Abstractions;

public interface IMigrationEngine
{
    Task<MigrationResult> MigrateAsync(
        MigrationRequest request,
        IProgress<MigrationProgress>? progress = null,
        CancellationToken cancellationToken = default);

    Task<string> GenerateSqlScriptAsync(MigrationRequest request, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<string>> GetTableNamesAsync(ConnectionProfile profile, CancellationToken cancellationToken = default);
}

