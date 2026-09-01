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

    [Fact]
    public void DrainsRingsThatAreMixedAtGainZero()
    {
        var muted = new PcmRingBuffer(64);
        var audible = new PcmRingBuffer(64);
        muted.Write(Bytes([1000, 2000]), CancellationToken.None);
        audible.Write(Bytes([10, 20]), CancellationToken.None);

        var mixer = new PcmMixSource();
        mixer.SetInputs([(muted, 0f), (audible, 1f)]);

        var destination = new byte[4];
        Assert.Equal(4, mixer.Read(destination));
        Assert.Equal(new short[] { 10, 20 }, Samples(destination));
        Assert.Equal(0, muted.Available);
    }

    [Fact]
    public void MixesFourRingsAtDifferentGains()
    {
        var rings = new PcmRingBuffer[4];
        for (var i = 0; i < rings.Length; i++)
        {
            rings[i] = new PcmRingBuffer(64);
            rings[i].Write(Bytes([1000, -800]), CancellationToken.None);
        }

        var mixer = new PcmMixSource();
        mixer.SetInputs([(rings[0], 1f), (rings[1], 0.5f), (rings[2], 0.25f), (rings[3], 0f)]);

        var destination = new byte[4];
        Assert.Equal(4, mixer.Read(destination));
        Assert.Equal(new short[] { 1750, -1400 }, Samples(destination));
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
