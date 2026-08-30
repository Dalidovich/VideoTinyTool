using VideoTinyTool.Media;

namespace VideoTinyTool.Tests.Media;

public class PcmMixerTests
{
    [Fact]
    public void SumsSamplesOfEveryInput()
    {
        var destination = new byte[4];

        PcmMixer.Mix(destination, [Input([100, -200], 1f), Input([50, 25], 1f)]);

        Assert.Equal(new short[] { 150, -175 }, Samples(destination));
    }

    [Fact]
    public void AppliesPerInputGain()
    {
        var destination = new byte[2];

        PcmMixer.Mix(destination, [Input([1000], 1f), Input([1000], 0.5f)]);

        Assert.Equal(new short[] { 1500 }, Samples(destination));
    }

    [Fact]
    public void SaturatesInsteadOfWrappingAround()
    {
        var destination = new byte[4];

        PcmMixer.Mix(destination, [Input([30000, -30000], 1f), Input([30000, -30000], 1f)]);

        Assert.Equal(new short[] { short.MaxValue, short.MinValue }, Samples(destination));
    }

    [Fact]
    public void TreatsMissingSamplesOfShorterInputsAsSilence()
    {
        var destination = new byte[6];

        PcmMixer.Mix(destination, [Input([10, 20, 30], 1f), Input([5], 1f)]);

        Assert.Equal(new short[] { 15, 20, 30 }, Samples(destination));
    }

    [Fact]
    public void FillsWithSilenceWhenEveryInputIsEmpty()
    {
        var destination = new byte[4];
        Array.Fill(destination, (byte)0x7F);

        PcmMixer.Mix(destination, [Input([], 1f), Input([], 1f)]);

        Assert.Equal(new short[] { 0, 0 }, Samples(destination));
    }

    [Fact]
    public void FillsWithSilenceWhenThereAreNoInputs()
    {
        var destination = new byte[4];
        Array.Fill(destination, (byte)0x7F);

        PcmMixer.Mix(destination, []);

        Assert.Equal(new short[] { 0, 0 }, Samples(destination));
    }

    [Fact]
    public void PassesASingleInputThrough()
    {
        var samples = new short[] { 0, 1, -1, 12345, -12345 };
        var destination = new byte[samples.Length * 2];

        PcmMixer.Mix(destination, [Input(samples, 1f)]);

        Assert.Equal(samples, Samples(destination));
    }

    [Fact]
    public void IgnoresATrailingOddByte()
    {
        var destination = new byte[3];

        PcmMixer.Mix(destination, [Input([777], 1f)]);

        Assert.Equal(new short[] { 777 }, Samples(destination.AsSpan(0, 2).ToArray()));
        Assert.Equal(0, destination[2]);
    }

    private static PcmMixInput Input(short[] samples, float gain)
    {
        var bytes = new byte[samples.Length * 2];
        Buffer.BlockCopy(samples, 0, bytes, 0, bytes.Length);
        return new PcmMixInput(bytes, gain);
    }

    private static short[] Samples(byte[] bytes)
    {
        var samples = new short[bytes.Length / 2];
        Buffer.BlockCopy(bytes, 0, samples, 0, samples.Length * 2);
        return samples;
    }
}
