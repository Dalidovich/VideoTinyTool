namespace VideoTinyTool.Media;

public sealed class PcmRingBuffer
{
    private readonly byte[] _buffer;
    private readonly object _gate = new();
    private int _readIndex;
    private int _available;
    private bool _closed;

    public PcmRingBuffer(int capacityBytes)
    {
        _buffer = new byte[capacityBytes];
    }

    public int Capacity => _buffer.Length;

    public int Available
    {
        get
        {
            lock (_gate)
            {
                return _available;
            }
        }
    }

    public void Write(ReadOnlySpan<byte> data, CancellationToken cancellation)
    {
        var offset = 0;
        while (offset < data.Length)
        {
            int chunk;
            lock (_gate)
            {
                while (!_closed && _available == _buffer.Length)
                {
                    if (cancellation.IsCancellationRequested)
                    {
                        throw new OperationCanceledException();
                    }

                    Monitor.Wait(_gate, 20);
                }

                if (_closed)
                {
                    return;
                }

                var writeIndex = (_readIndex + _available) % _buffer.Length;
                var free = _buffer.Length - _available;
                chunk = Math.Min(Math.Min(free, data.Length - offset), _buffer.Length - writeIndex);
                data.Slice(offset, chunk).CopyTo(_buffer.AsSpan(writeIndex, chunk));
                _available += chunk;
                Monitor.PulseAll(_gate);
            }

            offset += chunk;
        }
    }

    public int Read(Span<byte> destination)
    {
        lock (_gate)
        {
            var total = Math.Min(destination.Length, _available);
            var copied = 0;
            while (copied < total)
            {
                var chunk = Math.Min(total - copied, _buffer.Length - _readIndex);
                _buffer.AsSpan(_readIndex, chunk).CopyTo(destination.Slice(copied, chunk));
                _readIndex = (_readIndex + chunk) % _buffer.Length;
                _available -= chunk;
                copied += chunk;
            }

            Monitor.PulseAll(_gate);
            return copied;
        }
    }

    public void Clear()
    {
        lock (_gate)
        {
            _readIndex = 0;
            _available = 0;
            Monitor.PulseAll(_gate);
        }
    }

    public void Close()
    {
        lock (_gate)
        {
            _closed = true;
            Monitor.PulseAll(_gate);
        }
    }

    public void Reopen()
    {
        lock (_gate)
        {
            _closed = false;
            _readIndex = 0;
            _available = 0;
            Monitor.PulseAll(_gate);
        }
    }
}
