using System.Collections.Concurrent;
using System.Globalization;
using NReco.VideoConverter;
using SFML.Graphics;
using VideoTinyTool.Domain;

namespace VideoTinyTool.Media;

public sealed class ThumbnailService : IDisposable
{
    public const int ThumbnailHeight = 96;

    private const int MaxTextures = 300;
    private const int MaxTexturesPerFrame = 6;

    private readonly record struct Key(Guid SourceId, int HalfSeconds);

    private sealed record Request(Key Key, string Path, TimeSpan Offset, int Height);

    private readonly BlockingCollection<Request> _queue = new(new ConcurrentQueue<Request>());
    private readonly ConcurrentQueue<(Key Key, byte[]? Png)> _completed = new();
    private readonly HashSet<Key> _pending = new();
    private readonly Dictionary<Key, LinkedListNode<Key>> _lruIndex = new();
    private readonly LinkedList<Key> _lru = new();
    private readonly Dictionary<Key, Texture?> _cache = new();
    private readonly Thread _worker;
    private volatile bool _disposed;

    public ThumbnailService()
    {
        _worker = new Thread(Work)
        {
            IsBackground = true,
            Name = "VideoTinyTool.Thumbnails"
        };
        _worker.Start();
    }

    public Texture? Get(MediaSource source, TimeSpan offset)
    {
        var clamped = Clamp(source, offset);
        var key = new Key(source.Id, (int)Math.Round(clamped.TotalSeconds * 2));

        if (_cache.TryGetValue(key, out var texture))
        {
            Touch(key);
            return texture;
        }

        lock (_pending)
        {
            if (!_pending.Add(key))
            {
                return null;
            }
        }

        _queue.Add(new Request(key, source.Path, clamped, ThumbnailHeight));
        return null;
    }

    public Texture? GetPoster(MediaSource source) =>
        Get(source, source.Duration / 2 < TimeSpan.FromSeconds(1) ? source.Duration / 2 : TimeSpan.FromSeconds(1));

    public void PumpCompleted()
    {
        var created = 0;
        while (created < MaxTexturesPerFrame && _completed.TryDequeue(out var item))
        {
            Texture? texture = null;
            if (item.Png is { Length: > 0 })
            {
                try
                {
                    using var stream = new MemoryStream(item.Png);
                    texture = new Texture(stream) { Smooth = true };
                }
                catch (Exception)
                {
                    texture = null;
                }
            }

            Store(item.Key, texture);
            created++;
        }
    }

    private void Store(Key key, Texture? texture)
    {
        if (_cache.ContainsKey(key))
        {
            _cache[key]?.Dispose();
            _cache[key] = texture;
            Touch(key);
            return;
        }

        _cache[key] = texture;
        _lruIndex[key] = _lru.AddLast(key);

        while (_lru.Count > MaxTextures && _lru.First is { } oldest)
        {
            _lru.RemoveFirst();
            _lruIndex.Remove(oldest.Value);
            if (_cache.Remove(oldest.Value, out var evicted))
            {
                evicted?.Dispose();
            }
        }
    }

    private void Touch(Key key)
    {
        if (_lruIndex.TryGetValue(key, out var node))
        {
            _lru.Remove(node);
            _lru.AddLast(node);
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

        foreach (var request in _queue.GetConsumingEnumerable())
        {
            if (_disposed)
            {
                return;
            }

            byte[]? png = null;
            try
            {
                using var buffer = new MemoryStream();
                var settings = new ConvertSettings
                {
                    CustomOutputArgs = string.Format(
                        CultureInfo.InvariantCulture,
                        "-vf scale=-2:{0} -q:v 4",
                        request.Height)
                };

                converter.GetVideoThumbnail(request.Path, buffer, (float)request.Offset.TotalSeconds, settings);
                png = buffer.ToArray();
            }
            catch (Exception)
            {
                png = null;
            }
            finally
            {
                lock (_pending)
                {
                    _pending.Remove(request.Key);
                }

                _completed.Enqueue((request.Key, png));
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
        _queue.CompleteAdding();
        _worker.Join(TimeSpan.FromSeconds(3));

        foreach (var texture in _cache.Values)
        {
            texture?.Dispose();
        }

        _cache.Clear();
        _lru.Clear();
        _lruIndex.Clear();
    }
}
