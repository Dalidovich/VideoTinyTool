using VideoTinyTool.Media;

namespace VideoTinyTool.Tests.Media;

public class WaveformPeaksTests
{
    private const int SampleRate = 8000;
    private const int BucketsPerSecond = 100;
    private const int SamplesPerBucket = SampleRate / BucketsPerSecond;

    private static byte[] Pcm(params short[] samples)
    {
        var bytes = new byte[samples.Length * 2];
        for (var i = 0; i < samples.Length; i++)
        {
            bytes[i * 2] = (byte)(samples[i] & 0xFF);
            bytes[(i * 2) + 1] = (byte)((samples[i] >> 8) & 0xFF);
        }

        return bytes;
    }

    private static short[] Constant(short value, int count)
    {
        var samples = new short[count];
        Array.Fill(samples, value);
        return samples;
    }

    [Fact]
    public void EmptyStreamProducesNoBuckets()
    {
        var accumulator = new PeakAccumulator(SampleRate, BucketsPerSecond);
        Assert.Empty(accumulator.Complete());
    }

    [Fact]
    public void OneSecondProducesOneBucketPerHundredth()
    {
        var accumulator = new PeakAccumulator(SampleRate, BucketsPerSecond);
        accumulator.Append(Pcm(Constant(0, SampleRate)));

        Assert.Equal(BucketsPerSecond, accumulator.Complete().Length);
    }

    [Fact]
    public void EachBucketKeepsItsOwnPeak()
    {
        var samples = new short[SamplesPerBucket * 3];
        samples[5] = short.MaxValue;
        samples[SamplesPerBucket + 7] = 16384;
        samples[(SamplesPerBucket * 2) + 1] = 0;

        var accumulator = new PeakAccumulator(SampleRate, BucketsPerSecond);
        accumulator.Append(Pcm(samples));

        Assert.Equal(new byte[] { 255, 127, 0 }, accumulator.Complete());
    }

    [Fact]
    public void PeakIgnoresSignAndSaturatesAtFullScale()
    {
        var samples = Constant(0, SamplesPerBucket * 2);
        samples[0] = short.MaxValue;
        samples[SamplesPerBucket] = short.MinValue;

        var accumulator = new PeakAccumulator(SampleRate, BucketsPerSecond);
        accumulator.Append(Pcm(samples));

        Assert.Equal(new byte[] { 255, 255 }, accumulator.Complete());
    }

    [Fact]
    public void TrailingPartialBucketIsFlushedOnComplete()
    {
        var samples = Constant(0, SamplesPerBucket + 3);
        samples[SamplesPerBucket + 1] = short.MaxValue;

        var accumulator = new PeakAccumulator(SampleRate, BucketsPerSecond);
        accumulator.Append(Pcm(samples));

        Assert.Equal(new byte[] { 0, 255 }, accumulator.Complete());
    }

    [Fact]
    public void SamplesSplitAcrossWritesAreFoldedIntact()
    {
        var samples = Constant(0, SamplesPerBucket);
        samples[0] = short.MaxValue;
        var pcm = Pcm(samples);

        var accumulator = new PeakAccumulator(SampleRate, BucketsPerSecond);
        accumulator.Append(pcm.AsSpan(0, 1));
        accumulator.Append(pcm.AsSpan(1));

        Assert.Equal(new byte[] { 255 }, accumulator.Complete());
    }

    [Fact]
    public void CompleteAfterFlushingLeavesNothingBehind()
    {
        var accumulator = new PeakAccumulator(SampleRate, BucketsPerSecond);
        accumulator.Append(Pcm(Constant(short.MaxValue, SamplesPerBucket)));

        Assert.Equal(new byte[] { 255 }, accumulator.Complete());
        Assert.Equal(new byte[] { 255 }, accumulator.Complete());
    }
}
