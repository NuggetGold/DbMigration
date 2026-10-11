namespace DbMigration.Core.Persistence;

public sealed class FileSettingsStore : ISettingsStore
{
    private AppSettings _current;

    public FileSettingsStore(string dataDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dataDirectory);
        StorePath = Path.Combine(dataDirectory, "settings.json");
        _current = new AppSettings();
        Load();
    }

    public string StorePath { get; }

    public AppSettings Current => _current;

    public AppSettings Load()
    {
        _current = JsonFileStore.Read<AppSettings>(StorePath) ?? new AppSettings();
        return _current;
    }

    public void Save(AppSettings? settings = null)
    {
        var target = settings ?? _current;
        JsonFileStore.WriteAtomic(StorePath, target);
        _current = target;
    }
}

