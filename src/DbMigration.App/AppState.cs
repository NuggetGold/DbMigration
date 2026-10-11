using System.ComponentModel;

namespace DbMigration.App;

public sealed class AppState
{
    public AppState(DbMigrationServices services)
    {
        Services = services ?? throw new ArgumentNullException(nameof(services));
    }

    public DbMigrationServices Services { get; }

    public ActivityLog Activity { get; } = new();

    public AppSettings Settings => Services.Settings.Current;

    public BindingList<ConnectionProfile> Profiles { get; } = new();

    public ConnectionProfile? SourceProfile { get; private set; }

    public ConnectionProfile? TargetProfile { get; private set; }

    public event EventHandler? ProfilesChanged;

    public event EventHandler? SelectionChanged;

    public void Log(LogLevel level, string message) => Activity.Add(level, message);

    public async Task LoadProfilesAsync(CancellationToken cancellationToken = default)
    {
        var loaded = await Services.Profiles.LoadAsync(cancellationToken);

        Profiles.RaiseListChangedEvents = false;
        try
        {
            Profiles.Clear();
            foreach (var profile in loaded)
            {
                if (!string.IsNullOrEmpty(profile.ProtectedPassword))
                {
                    profile.Password = Services.Secrets.Unprotect(profile.ProtectedPassword);
                }

                Profiles.Add(profile);
            }
        }
        finally
        {
            Profiles.RaiseListChangedEvents = true;
        }

        Profiles.ResetBindings();
        ProfilesChanged?.Invoke(this, EventArgs.Empty);
    }

    public async Task SaveProfilesAsync(CancellationToken cancellationToken = default)
    {
        foreach (var profile in Profiles)
        {
            profile.ProtectedPassword = Services.Secrets.Protect(profile.Password);
        }

        await Services.Profiles.SaveAsync(Profiles.ToArray(), cancellationToken);
        ProfilesChanged?.Invoke(this, EventArgs.Empty);
    }

    public async Task UpsertProfileAsync(ConnectionProfile profile, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(profile);

        int index = -1;
        for (int i = 0; i < Profiles.Count; i++)
        {
            if (Profiles[i].Id == profile.Id)
            {
                index = i;
                break;
            }
        }

        if (index >= 0)
        {
            Profiles[index] = profile;
        }
        else
        {
            Profiles.Add(profile);
        }

        await SaveProfilesAsync(cancellationToken);
        Log(LogLevel.Info, $"Подключение «{profile.Name}» сохранено.");
    }

    public async Task RemoveProfileAsync(ConnectionProfile profile, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(profile);

        if (ReferenceEquals(SourceProfile, profile))
        {
            SetSource(null);
        }

        if (ReferenceEquals(TargetProfile, profile))
        {
            SetTarget(null);
        }

        Profiles.Remove(profile);
        await SaveProfilesAsync(cancellationToken);
        Log(LogLevel.Info, $"Подключение «{profile.Name}» удалено.");
    }

    public void SetSource(ConnectionProfile? profile)
    {
        SourceProfile = profile;
        SelectionChanged?.Invoke(this, EventArgs.Empty);
    }

    public void SetTarget(ConnectionProfile? profile)
    {
        TargetProfile = profile;
        SelectionChanged?.Invoke(this, EventArgs.Empty);
    }

    public void NotifyProfilesChanged() => ProfilesChanged?.Invoke(this, EventArgs.Empty);
}

