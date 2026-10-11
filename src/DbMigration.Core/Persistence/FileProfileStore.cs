namespace DbMigration.Core.Persistence;

public sealed class FileProfileStore : IProfileStore
{
    private readonly ISecretProtector _protector;

    public FileProfileStore(string dataDirectory, ISecretProtector protector)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dataDirectory);
        _protector = protector ?? throw new ArgumentNullException(nameof(protector));
        StorePath = Path.Combine(dataDirectory, "profiles.json");
    }

    public string StorePath { get; }

    public async Task<IReadOnlyList<ConnectionProfile>> LoadAsync(CancellationToken cancellationToken = default)
    {
        var profiles = await JsonFileStore
            .ReadAsync<List<ConnectionProfile>>(StorePath, cancellationToken)
            .ConfigureAwait(false);

        if (profiles is null || profiles.Count == 0)
        {
            return Array.Empty<ConnectionProfile>();
        }

        foreach (var profile in profiles)
        {
            if (!string.IsNullOrEmpty(profile.ProtectedPassword))
            {
                profile.Password = _protector.Unprotect(profile.ProtectedPassword);
            }
        }

        return profiles;
    }

    public Task SaveAsync(IEnumerable<ConnectionProfile> profiles, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(profiles);

        var snapshot = profiles.Select(ToPersistable).ToList();
        return JsonFileStore.WriteAtomicAsync(StorePath, snapshot, cancellationToken);
    }

    private ConnectionProfile ToPersistable(ConnectionProfile profile)
    {
        var clone = profile.Clone();
        if (!string.IsNullOrEmpty(clone.Password))
        {
            clone.ProtectedPassword = _protector.Protect(clone.Password);
        }

        clone.Password = null;
        return clone;
    }
}

