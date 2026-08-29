using System.Diagnostics;

namespace VideoTinyTool.Media;

public sealed class PlaybackClock
{
    private readonly Stopwatch _stopwatch = new();
    private TimeSpan _base;
    private double _rate = 1.0;
    private Func<TimeSpan>? _audioOffset;

    public bool AudioDriven => _audioOffset is not null;

    public double Rate => _rate;

    public void StartSystem(TimeSpan position, double rate)
    {
        _audioOffset = null;
        _base = position;
        _rate = rate;
        _stopwatch.Restart();
    }

    public void StartAudio(TimeSpan position, Func<TimeSpan> audioOffset)
    {
        _audioOffset = audioOffset;
        _base = position;
        _rate = 1.0;
        _stopwatch.Restart();
    }

    public void Stop()
    {
        _base = Position;
        _audioOffset = null;
        _stopwatch.Reset();
    }

    public void Reset(TimeSpan position)
    {
        _base = position;
        _audioOffset = null;
        _stopwatch.Reset();
    }

    public TimeSpan Position
    {
        get
        {
            if (_audioOffset is not null)
            {
                return _base + _audioOffset();
            }

            return _stopwatch.IsRunning
                ? _base + TimeSpan.FromTicks((long)(_stopwatch.Elapsed.Ticks * _rate))
                : _base;
        }
    }
}
