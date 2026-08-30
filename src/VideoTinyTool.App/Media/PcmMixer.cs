namespace VideoTinyTool.Media;

public readonly record struct PcmMixInput(ReadOnlyMemory<byte> Samples, float Gain);

public static class PcmMixer
{
    public static void Mix(Span<byte> destination, IReadOnlyList<PcmMixInput> inputs)
    {
        destination.Clear();

        if (inputs.Count == 0)
        {
            return;
        }

        var samples = destination.Length / 2;
        for (var i = 0; i < samples; i++)
        {
            var offset = i * 2;
            var sum = 0;

            for (var k = 0; k < inputs.Count; k++)
            {
                var span = inputs[k].Samples.Span;
                if (offset + 1 >= span.Length)
                {
                    continue;
                }

                var sample = (short)(span[offset] | (span[offset + 1] << 8));
                sum += (int)MathF.Round(sample * inputs[k].Gain);
            }

            var clamped = (short)Math.Clamp(sum, short.MinValue, short.MaxValue);
            destination[offset] = (byte)(clamped & 0xFF);
            destination[offset + 1] = (byte)((clamped >> 8) & 0xFF);
        }
    }
}
