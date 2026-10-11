namespace DbMigration.Core.Abstractions;

public interface IDatabaseProviderFactory
{
    IReadOnlyList<IDatabaseProvider> All { get; }

    IDatabaseProvider Get(DatabaseEngine engine);

    bool TryGet(DatabaseEngine engine, out IDatabaseProvider provider);
}

