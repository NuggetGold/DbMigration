using System.Diagnostics;

namespace DbMigration.App.Theme;

public sealed class AnimationClock : IDisposable
{
    private readonly System.Windows.Forms.Timer _timer;
    private readonly Stopwatch _stopwatch = new();
    private int _fps = 60;

    private AnimationClock()
    {
        _timer = new System.Windows.Forms.Timer { Interval = 1000 / 60 };
        _timer.Tick += (_, _) =>
        {
            Time = _stopwatch.Elapsed.TotalSeconds;
            Tick?.Invoke(this, EventArgs.Empty);
        };
    }

    public static AnimationClock Instance { get; } = new();

    public event EventHandler? Tick;

    public double Time { get; private set; }

    public int Fps
    {
        get => _fps;
        set
        {
            int clamped = Math.Clamp(value, 15, 120);
            if (_fps == clamped)
            {
                return;
            }

            _fps = clamped;
            _timer.Interval = Math.Max(1, 1000 / _fps);
        }
    }

    public bool Running => _timer.Enabled;

    public void Start()
    {
        if (_timer.Enabled)
        {
            return;
        }

        _stopwatch.Start();
        _timer.Start();
    }

    public void Stop()
    {
        if (!_timer.Enabled)
        {
            return;
        }

        _timer.Stop();
        _stopwatch.Stop();
    }

    public void Advance() => Time = _stopwatch.Elapsed.TotalSeconds;

    public void Dispose()
    {
        _timer.Stop();
        _timer.Dispose();
    }
}

