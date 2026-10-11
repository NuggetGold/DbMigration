using System.Text;

namespace DbMigration.App;

public sealed class ActivityLog
{
    private readonly List<LogEntry> _entries = new();
    private readonly object _gate = new();
    private const int Capacity = 5000;

    public event EventHandler<LogEntry>? EntryAdded;

    public void Add(LogLevel level, string message) => Add(new LogEntry(DateTimeOffset.Now, level, message ?? string.Empty));

    public void Add(LogEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        lock (_gate)
        {
            _entries.Add(entry);
            if (_entries.Count > Capacity)
            {
                _entries.RemoveRange(0, _entries.Count - Capacity);
            }
        }

        EntryAdded?.Invoke(this, entry);
    }

    public IReadOnlyList<LogEntry> Snapshot()
    {
        lock (_gate)
        {
            return _entries.ToArray();
        }
    }

    public void Clear()
    {
        lock (_gate)
        {
            _entries.Clear();
        }
    }

    public string ToText()
    {
        var builder = new StringBuilder();
        foreach (var entry in Snapshot())
        {
            builder.Append('[').Append(entry.Timestamp.ToString("HH:mm:ss")).Append("] ")
                .Append(entry.Level.ToString().ToUpperInvariant().PadRight(7)).Append(' ')
                .AppendLine(entry.Message);
        }

        return builder.ToString();
    }
}

