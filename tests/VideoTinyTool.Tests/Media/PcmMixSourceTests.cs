using VideoTinyTool.Media;

namespace VideoTinyTool.Tests.Media;

public class PcmMixSourceTests
{
    [Fact]
    public void MixesRingsThatHoldDifferentAmountsOfData()
    {
        var loud = new PcmRingBuffer(64);
        var quiet = new PcmRingBuffer(64);
        loud.Write(Bytes([100, 200, 300]), CancellationToken.None);
        quiet.Write(Bytes([10]), CancellationToken.None);

        var mixer = new PcmMixSource();
        mixer.SetInputs([(loud, 1f), (quiet, 1f)]);

        var destination = new byte[6];
        Assert.Equal(6, mixer.Read(destination));
        Assert.Equal(new short[] { 110, 200, 300 }, Samples(destination));
    }

    [Fact]
    public void ReturnsSilenceWithoutStallingWhenEveryRingIsEmpty()
    {
        var mixer = new PcmMixSource();
        mixer.SetInputs([(new PcmRingBuffer(64), 1f), (new PcmRingBuffer(64), 1f)]);

        var destination = new byte[4];
        Assert.Equal(4, mixer.Read(destination));
        Assert.Equal(new short[] { 0, 0 }, Samples(destination));
    }

    private static byte[] Bytes(short[] samples)
    {
        var bytes = new byte[samples.Length * 2];
        Buffer.BlockCopy(samples, 0, bytes, 0, bytes.Length);
        return bytes;
    }

    private static short[] Samples(byte[] bytes)
    {
        var samples = new short[bytes.Length / 2];
        Buffer.BlockCopy(bytes, 0, samples, 0, samples.Length * 2);
        return samples;
    }
}
