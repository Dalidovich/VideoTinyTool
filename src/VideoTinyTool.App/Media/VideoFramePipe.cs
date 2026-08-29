using System.Collections.Concurrent;
using System.Globalization;
using NReco.VideoConverter;

namespace VideoTinyTool.Media;

public sealed class VideoFramePipe : IDisposable
{
    private const int QueueDepth = 8;

    private readonly int _frameBytes;
    private readonly BlockingCollection<byte[]> _frames = new(QueueDepth);
    private readonly CancellationTokenSource _cancellation = new();
    private readonly FFMpegConverter _converter = FFmpegRuntime.CreateConverter();
    private readonly FrameAssemblyStream _stream;

    private ConvertLiveMediaTask? _task;
    private volatile bool _disposed;

    public VideoFramePipe(string sourcePath, TimeSpan startOffset, int width, int height, double frameRate)
    {
        Width = width;
        Height = height;
        FrameRate = frameRate;
        _frameBytes = width * height * 4;
        _stream = new FrameAssemblyStream(_frameBytes, PushFrame);

        var settings = new ConvertSettings
        {
            CustomInputArgs = $"-ss {FFmpegArgumentBuilder.Seconds(startOffset)}",
            CustomOutputArgs = string.Format(
                CultureInfo.InvariantCulture,
                "-an -sn -dn -pix_fmt rgba -s {0}x{1} -r {2:0.###}",
                width,
                height,
                frameRate)
        };

        _task = _converter.ConvertLiveMedia(sourcePath, null, _stream, "rawvideo", settings);
    }

    public int Width { get; }

    public int Height { get; }

    public double FrameRate { get; }

    public int BufferedFrames => _frames.Count;

    public bool Ended => _stream.SourceEnded && _frames.Count == 0;

    public void Start()
    {
        try
        {
            _task?.Start();
        }
        catch (Exception)
        {
            _stream.MarkEnded();
        }
    }

    public bool TryTakeFrame(out byte[] frame) => _frames.TryTake(out frame!);

    private void PushFrame(byte[] frame)
    {
        try
        {
            _frames.Add(frame, _cancellation.Token);
        }
        catch (Exception) when (_cancellation.IsCancellationRequested)
        {
            throw new OperationCanceledException();
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _cancellation.Cancel();

        var task = _task;
        _task = null;

        if (task is not null)
        {
            var stopper = new Thread(() =>
            {
                try
                {
                    task.Stop(true);
                }
                catch
                {
                    // The process is going away regardless.
                }

                try
                {
                    _converter.Abort();
                }
                catch
                {
                    // Already gone.
                }
            })
            {
                IsBackground = true,
                Name = "VideoTinyTool.VideoPipeStop"
            };
            stopper.Start();
        }

        while (_frames.TryTake(out _))
        {
        }

        _frames.Dispose();
        _cancellation.Dispose();
    }

    private sealed class FrameAssemblyStream : Stream
    {
        private readonly int _frameBytes;
        private readonly Action<byte[]> _onFrame;
        private byte[] _pending;
        private int _pendingLength;

        public FrameAssemblyStream(int frameBytes, Action<byte[]> onFrame)
        {
            _frameBytes = frameBytes;
            _onFrame = onFrame;
            _pending = new byte[frameBytes];
        }

        public volatile bool SourceEnded;

        public void MarkEnded() => SourceEnded = true;

        public override bool CanRead => false;
        public override bool CanSeek => false;
        public override bool CanWrite => true;
        public override long Length => 0;

        public override long Position
        {
            get => 0;
            set { }
        }

        public override void Flush()
        {
        }

        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count)
        {
            var consumed = 0;
            while (consumed < count)
            {
                var chunk = Math.Min(_frameBytes - _pendingLength, count - consumed);
                Buffer.BlockCopy(buffer, offset + consumed, _pending, _pendingLength, chunk);
                _pendingLength += chunk;
                consumed += chunk;

                if (_pendingLength == _frameBytes)
                {
                    _onFrame(_pending);
                    _pending = new byte[_frameBytes];
                    _pendingLength = 0;
                }
            }
        }

        public override void Close()
        {
            SourceEnded = true;
            base.Close();
        }
    }
}
