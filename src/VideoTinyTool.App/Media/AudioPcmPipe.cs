using NReco.VideoConverter;

namespace VideoTinyTool.Media;

public sealed class AudioPcmPipe : IDisposable
{
    public const int SampleRate = 48000;
    public const int Channels = 2;
    public const int BytesPerSample = 2;

    private readonly CancellationTokenSource _cancellation = new();
    private readonly FFMpegConverter _converter = FFmpegRuntime.CreateConverter();
    private readonly RingWriterStream _stream;

    private ConvertLiveMediaTask? _task;
    private volatile bool _disposed;

    public AudioPcmPipe(string sourcePath, TimeSpan startOffset, PcmRingBuffer ring)
    {
        _stream = new RingWriterStream(ring, _cancellation.Token);

        var settings = new ConvertSettings
        {
            CustomInputArgs = $"-ss {FFmpegArgumentBuilder.Seconds(startOffset)}",
            CustomOutputArgs = $"-vn -sn -dn -ar {SampleRate} -ac {Channels}"
        };

        _task = _converter.ConvertLiveMedia(sourcePath, null, _stream, "s16le", settings);
    }

    public bool SourceEnded => _stream.SourceEnded;

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
                Name = "VideoTinyTool.AudioPipeStop"
            };
            stopper.Start();
        }

        _cancellation.Dispose();
    }

    private sealed class RingWriterStream : Stream
    {
        private readonly PcmRingBuffer _ring;
        private readonly CancellationToken _cancellation;

        public RingWriterStream(PcmRingBuffer ring, CancellationToken cancellation)
        {
            _ring = ring;
            _cancellation = cancellation;
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

        public override void Write(byte[] buffer, int offset, int count) =>
            _ring.Write(buffer.AsSpan(offset, count), _cancellation);

        public override void Close()
        {
            SourceEnded = true;
            base.Close();
        }
    }
}
