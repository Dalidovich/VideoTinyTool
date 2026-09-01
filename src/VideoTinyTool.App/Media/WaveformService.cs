using System.Collections.Concurrent;
using NReco.VideoConverter;
using VideoTinyTool.Domain;

namespace VideoTinyTool.Media;

public sealed class PeakAccumulator
{
    private readonly int _samplesPerBucket;
    private readonly List<byte> _peaks = new();

    private int _samplesInBucket;
    private int _bucketPeak;
    private byte _carry;
    private bool _hasCarry;

    public PeakAccumulator(int sampleRate, int bucketsPerSecond)
    {
        if (sampleRate <= 0 || bucketsPerSecond <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(sampleRate));
        }

        _samplesPerBucket = Math.Max(1, sampleRate / bucketsPerSecond);
    }

    public void Append(ReadOnlySpan<byte> pcm)
    {
        var index = 0;

        if (_hasCarry && pcm.Length > 0)
        {
            Fold((short)(_carry | (pcm[0] << 8)));
            _hasCarry = false;
            index = 1;
        }

        for (; index + 1 < pcm.Length; index += 2)
        {
            Fold((short)(pcm[index] | (pcm[index + 1] << 8)));
        }

        if (index < pcm.Length)
        {
            _carry = pcm[index];
            _hasCarry = true;
        }
    }

    public byte[] Complete()
    {
        if (_samplesInBucket > 0)
        {
            _peaks.Add(Scale(_bucketPeak));
            _samplesInBucket = 0;
            _bucketPeak = 0;
        }

        return _peaks.ToArray();
    }

    private void Fold(short sample)
    {
        var magnitude = sample == short.MinValue ? -(int)short.MinValue : Math.Abs((int)sample);
        if (magnitude > _bucketPeak)
        {
            _bucketPeak = magnitude;
        }

        if (++_samplesInBucket < _samplesPerBucket)
        {
            return;
        }

        _peaks.Add(Scale(_bucketPeak));
        _samplesInBucket = 0;
        _bucketPeak = 0;
    }

    private static byte Scale(int magnitude) =>
        (byte)Math.Min(255, magnitude * 255 / short.MaxValue);
}

public sealed class WaveformService : IDisposable
{
    public const int BucketsPerSecond = 100;
    public const int SampleRate = 8000;

    private const int MaxSources = 24;
    private const int MaxPublishedPerFrame = 4;

    private sealed record Request(Guid SourceId, string Path);

    private readonly BlockingCollection<Request> _queue = new(new ConcurrentQueue<Request>());
    private readonly ConcurrentQueue<(Guid SourceId, byte[] Peaks)> _completed = new();
    private readonly HashSet<Guid> _pending = new();
    private readonly Dictionary<Guid, byte[]> _cache = new();
    private readonly Dictionary<Guid, LinkedListNode<Guid>> _lruIndex = new();
    private readonly LinkedList<Guid> _lru = new();
    private readonly Thread _worker;

    private volatile FFMpegConverter? _converter;
    private volatile ConvertLiveMediaTask? _running;
    private volatile bool _disposed;

    public WaveformService()
    {
        _worker = new Thread(Work)
        {
            IsBackground = true,
            Name = "VideoTinyTool.Waveforms"
        };
        _worker.Start();
    }

    public byte[]? Get(MediaSource source)
    {
        if (_cache.TryGetValue(source.Id, out var peaks))
        {
            Touch(source.Id);
            return peaks;
        }

        lock (_pending)
        {
            if (!_pending.Add(source.Id))
            {
                return null;
            }
        }

        _queue.Add(new Request(source.Id, source.Path));
        return null;
    }

    public void PumpCompleted()
    {
        var published = 0;
        while (published < MaxPublishedPerFrame && _completed.TryDequeue(out var item))
        {
            Store(item.SourceId, item.Peaks);
            published++;
        }
    }

    public void Forget(Guid sourceId)
    {
        _cache.Remove(sourceId);
        if (_lruIndex.Remove(sourceId, out var node))
        {
            _lru.Remove(node);
        }
    }

    private void Store(Guid sourceId, byte[] peaks)
    {
        if (_cache.ContainsKey(sourceId))
        {
            _cache[sourceId] = peaks;
            Touch(sourceId);
            return;
        }

        _cache[sourceId] = peaks;
        _lruIndex[sourceId] = _lru.AddLast(sourceId);

        while (_lru.Count > MaxSources && _lru.First is { } oldest)
        {
            _lru.RemoveFirst();
            _lruIndex.Remove(oldest.Value);
            _cache.Remove(oldest.Value);
        }
    }

    private void Touch(Guid sourceId)
    {
        if (_lruIndex.TryGetValue(sourceId, out var node))
        {
            _lru.Remove(node);
            _lru.AddLast(node);
        }
    }

    private void Work()
    {
        _converter = FFmpegRuntime.CreateConverter();

        foreach (var request in _queue.GetConsumingEnumerable())
        {
            if (_disposed)
            {
                return;
            }

            byte[] peaks = [];
            try
            {
                peaks = Analyse(request.Path);
            }
            catch (Exception)
            {
                peaks = [];
            }
            finally
            {
                lock (_pending)
                {
                    _pending.Remove(request.SourceId);
                }

                _completed.Enqueue((request.SourceId, peaks));
            }
        }
    }

    private byte[] Analyse(string path)
    {
        var accumulator = new PeakAccumulator(SampleRate, BucketsPerSecond);
        using var sink = new PeakSinkStream(accumulator);

        var settings = new ConvertSettings
        {
            CustomOutputArgs = $"-vn -sn -dn -ac 1 -ar {SampleRate}"
        };

        var task = _converter!.ConvertLiveMedia(path, null, sink, "s16le", settings);
        _running = task;

        try
        {
            task.Start();
            task.Wait();
        }
        finally
        {
            _running = null;
        }

        return _disposed ? [] : accumulator.Complete();
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _queue.CompleteAdding();

        var task = _running;
        if (task is not null)
        {
            try
            {
                task.Stop(true);
            }
            catch
            {
            }
        }

        try
        {
            _converter?.Abort();
        }
        catch
        {
        }

        _worker.Join(TimeSpan.FromSeconds(3));

        _cache.Clear();
        _lru.Clear();
        _lruIndex.Clear();
    }

    private sealed class PeakSinkStream : Stream
    {
        private readonly PeakAccumulator _accumulator;

        public PeakSinkStream(PeakAccumulator accumulator) => _accumulator = accumulator;

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
            _accumulator.Append(buffer.AsSpan(offset, count));
    }
}
