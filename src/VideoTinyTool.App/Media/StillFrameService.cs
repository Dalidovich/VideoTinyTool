using System.Globalization;
using NReco.VideoConverter;
using VideoTinyTool.Domain;

namespace VideoTinyTool.Media;

public sealed record StillFrame(int Slot, Guid Token, byte[]? Png, double Aspect);

public sealed class StillFrameService : IDisposable
{
    private readonly object _gate = new();
    private readonly AutoResetEvent _signal = new(false);
    private readonly Thread _worker;
    private readonly Dictionary<int, (Guid Token, string Path, TimeSpan Offset, int Height, double Aspect)> _requested = new();
    private readonly Queue<StillFrame> _results = new();

    private volatile bool _disposed;

    public StillFrameService()
    {
        _worker = new Thread(Work)
        {
            IsBackground = true,
            Name = "VideoTinyTool.StillFrame"
        };
        _worker.Start();
    }

    public Guid Request(MediaSource source, TimeSpan sourceOffset, int height) =>
        Request(0, source, sourceOffset, height);

    public Guid Request(int slot, MediaSource source, TimeSpan sourceOffset, int height)
    {
        var token = Guid.NewGuid();
        lock (_gate)
        {
            _requested[slot] = (token, source.Path, Clamp(source, sourceOffset), height, source.AspectRatio);
        }

        _signal.Set();
        return token;
    }

    public StillFrame? TakeResult()
    {
        lock (_gate)
        {
            return _results.Count == 0 ? null : _results.Dequeue();
        }
    }

    private static TimeSpan Clamp(MediaSource source, TimeSpan offset)
    {
        var max = source.Duration - TimeSpan.FromMilliseconds(50);
        if (max < TimeSpan.Zero)
        {
            max = TimeSpan.Zero;
        }

        return offset < TimeSpan.Zero ? TimeSpan.Zero : offset > max ? max : offset;
    }

    private void Work()
    {
        var converter = FFmpegRuntime.CreateConverter();

        while (!_disposed)
        {
            _signal.WaitOne(200);
            if (_disposed)
            {
                return;
            }

            while (!_disposed && TryTakeJob(out var slot, out var job))
            {
                byte[]? png = null;
                try
                {
                    using var buffer = new MemoryStream();
                    var settings = new ConvertSettings
                    {
                        CustomOutputArgs = string.Format(
                            CultureInfo.InvariantCulture,
                            "-vf scale=-2:{0} -q:v 2",
                            job.Height)
                    };

                    converter.GetVideoThumbnail(job.Path, buffer, (float)job.Offset.TotalSeconds, settings);
                    png = buffer.ToArray();
                }
                catch (Exception)
                {
                    png = null;
                }

                lock (_gate)
                {
                    _results.Enqueue(new StillFrame(slot, job.Token, png, job.Aspect));
                }
            }
        }
    }

    private bool TryTakeJob(out int slot, out (Guid Token, string Path, TimeSpan Offset, int Height, double Aspect) job)
    {
        lock (_gate)
        {
            var lowest = int.MaxValue;
            foreach (var key in _requested.Keys)
            {
                if (key < lowest)
                {
                    lowest = key;
                }
            }

            if (lowest != int.MaxValue)
            {
                slot = lowest;
                job = _requested[lowest];
                _requested.Remove(lowest);
                return true;
            }
        }

        slot = 0;
        job = default;
        return false;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _signal.Set();

        if (_worker.Join(TimeSpan.FromSeconds(3)))
        {
            _signal.Dispose();
        }
    }
}
