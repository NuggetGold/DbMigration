namespace DbMigration.Core.Abstractions;

public interface ISecretProtector
{
    string? Protect(string? plaintext);

    string? Unprotect(string? protectedBlob);
}

public interface IProfileStore
{
    string StorePath { get; }

    Task<IReadOnlyList<ConnectionProfile>> LoadAsync(CancellationToken cancellationToken = default);

    Task SaveAsync(IEnumerable<ConnectionProfile> profiles, CancellationToken cancellationToken = default);
}

public interface ISettingsStore
{
    string StorePath { get; }

    AppSettings Current { get; }

    AppSettings Load();

    void Save(AppSettings? settings = null);
}

