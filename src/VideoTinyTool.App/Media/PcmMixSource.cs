namespace VideoTinyTool.Media;

public sealed class PcmMixSource : IPcmSource
{
    private readonly object _gate = new();
    private readonly List<(IPcmSource Source, float Gain)> _inputs = new();
    private readonly List<byte[]> _scratch = new();
    private readonly List<PcmMixInput> _chunks = new();

    public void SetInputs(IReadOnlyList<(IPcmSource Source, float Gain)> inputs)
    {
        lock (_gate)
        {
            _inputs.Clear();
            _inputs.AddRange(inputs);
        }
    }

    public int Read(Span<byte> destination)
    {
        lock (_gate)
        {
            _chunks.Clear();

            for (var i = 0; i < _inputs.Count; i++)
            {
                var buffer = Scratch(i, destination.Length);
                var read = _inputs[i].Source.Read(buffer);
                _chunks.Add(new PcmMixInput(buffer.AsMemory(0, read), _inputs[i].Gain));
            }

            PcmMixer.Mix(destination, _chunks);
            return destination.Length;
        }
    }

    private byte[] Scratch(int index, int length)
    {
        while (_scratch.Count <= index)
        {
            _scratch.Add([]);
        }

        if (_scratch[index].Length < length)
        {
            _scratch[index] = new byte[length];
        }

        return _scratch[index];
    }
}
