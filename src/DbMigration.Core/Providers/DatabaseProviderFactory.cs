namespace DbMigration.Core.Providers;

public sealed class DatabaseProviderFactory : IDatabaseProviderFactory
{
    private readonly Dictionary<DatabaseEngine, IDatabaseProvider> _byEngine;

    public DatabaseProviderFactory()
        : this(new IDatabaseProvider[]
        {
            new SqliteProvider(),
            new MySqlProvider(),
            new PostgreSqlProvider(),
            new SqlServerProvider(),
        })
    {
    }

    public DatabaseProviderFactory(IEnumerable<IDatabaseProvider> providers)
    {
        ArgumentNullException.ThrowIfNull(providers);

        var ordered = new List<IDatabaseProvider>();
        _byEngine = new Dictionary<DatabaseEngine, IDatabaseProvider>();
        foreach (var provider in providers)
        {
            ArgumentNullException.ThrowIfNull(provider);
            if (!_byEngine.ContainsKey(provider.Engine))
            {
                ordered.Add(provider);
            }

            _byEngine[provider.Engine] = provider;
        }

        All = ordered;
    }

    public IReadOnlyList<IDatabaseProvider> All { get; }

    public IDatabaseProvider Get(DatabaseEngine engine) =>
        TryGet(engine, out var provider)
            ? provider
            : throw new NotSupportedException($"No database provider is registered for engine '{engine}'.");

    public bool TryGet(DatabaseEngine engine, out IDatabaseProvider provider)
    {
        provider = default!;
        if (_byEngine.TryGetValue(engine, out var found))
        {
            provider = found;
            return true;
        }

        return false;
    }
}

