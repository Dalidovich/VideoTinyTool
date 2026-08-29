using System.Globalization;
using NReco.VideoConverter;
using VideoTinyTool.Domain;

namespace VideoTinyTool.Media;

public sealed record StillFrame(Guid Token, byte[]? Png, double Aspect);

public sealed class StillFrameService : IDisposable
{
    private readonly object _gate = new();
    private readonly AutoResetEvent _signal = new(false);
    private readonly Thread _worker;

    private (Guid Token, string Path, TimeSpan Offset, int Height, double Aspect)? _requested;
    private StillFrame? _result;
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

    public Guid Request(MediaSource source, TimeSpan sourceOffset, int height)
    {
        var token = Guid.NewGuid();
        lock (_gate)
        {
            _requested = (token, source.Path, Clamp(source, sourceOffset), height, source.AspectRatio);
        }

        _signal.Set();
        return token;
    }

    public StillFrame? TakeResult()
    {
        lock (_gate)
        {
            var result = _result;
            _result = null;
            return result;
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

            (Guid Token, string Path, TimeSpan Offset, int Height, double Aspect) job;
            lock (_gate)
            {
                if (_requested is null)
                {
                    continue;
                }

                job = _requested.Value;
                _requested = null;
            }

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
                _result = new StillFrame(job.Token, png, job.Aspect);
            }
        }
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
