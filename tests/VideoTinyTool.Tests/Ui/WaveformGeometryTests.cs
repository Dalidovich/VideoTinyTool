using VideoTinyTool.Ui;

namespace VideoTinyTool.Tests.Ui;

public class WaveformGeometryTests
{
    private static readonly byte[] Peaks = [10, 200, 30, 40, 255, 5];

    [Theory]
    [InlineData(0, 0)]
    [InlineData(9, 0)]
    [InlineData(10, 1)]
    [InlineData(1005, 100)]
    [InlineData(2999, 299)]
    public void BucketAtFloorsTheSourceTime(int milliseconds, int expected) =>
        Assert.Equal(expected, WaveformGeometry.BucketAt(TimeSpan.FromMilliseconds(milliseconds), 100));

    [Fact]
    public void BucketAtClampsNegativeTimeAndGuardsTheRate()
    {
        Assert.Equal(0, WaveformGeometry.BucketAt(TimeSpan.FromSeconds(-3), 100));
        Assert.Equal(0, WaveformGeometry.BucketAt(TimeSpan.FromSeconds(3), 0));
    }

    [Fact]
    public void PeakBetweenTakesTheMaximumOverTheRange()
    {
        Assert.Equal(200, WaveformGeometry.PeakBetween(Peaks, 0, 2));
        Assert.Equal(40, WaveformGeometry.PeakBetween(Peaks, 2, 3));
        Assert.Equal(30, WaveformGeometry.PeakBetween(Peaks, 2, 2));
    }

    [Fact]
    public void PeakBetweenClampsToTheArray()
    {
        Assert.Equal(255, WaveformGeometry.PeakBetween(Peaks, -20, 400));
        Assert.Equal(255, WaveformGeometry.PeakBetween(Peaks, 4, 900));
        Assert.Equal(10, WaveformGeometry.PeakBetween(Peaks, -5, 0));
    }

    [Fact]
    public void PeakBetweenIsZeroForEmptyAndReversedRanges()
    {
        Assert.Equal(0, WaveformGeometry.PeakBetween(ReadOnlySpan<byte>.Empty, 0, 5));
        Assert.Equal(0, WaveformGeometry.PeakBetween(Peaks, 3, 1));
        Assert.Equal(0, WaveformGeometry.PeakBetween(Peaks, 20, 40));
        Assert.Equal(0, WaveformGeometry.PeakBetween(Peaks, -9, -2));
    }

    [Fact]
    public void ColumnRangeCoversOneColumnWidthOfSourceTime()
    {
        var (from, to) = WaveformGeometry.ColumnRange(TimeSpan.FromSeconds(2), 0, 1f, 100f);

        Assert.Equal(TimeSpan.FromSeconds(2), from);
        Assert.Equal(TimeSpan.FromMilliseconds(2010), to);
    }

    [Theory]
    [InlineData(1f, 40f, 0.025)]
    [InlineData(1f, 4000f, 0.00025)]
    [InlineData(2f, 40f, 0.05)]
    public void ColumnRangeScalesWithZoomAndColumnWidth(float columnWidth, float pixelsPerSecond, double seconds)
    {
        var (from, to) = WaveformGeometry.ColumnRange(TimeSpan.Zero, 4, columnWidth, pixelsPerSecond);

        Assert.Equal(seconds * 4, from.TotalSeconds, 9);
        Assert.Equal(seconds * 5, to.TotalSeconds, 9);
    }

    [Fact]
    public void SubBucketColumnsCollapseOntoTheSameBucket()
    {
        var (from, to) = WaveformGeometry.ColumnRange(TimeSpan.Zero, 3, 1f, 4000f);

        Assert.Equal(0, WaveformGeometry.BucketAt(from, 100));
        Assert.Equal(0, WaveformGeometry.BucketAt(to, 100));
    }

    [Fact]
    public void ZoomedOutColumnsSpanManyBuckets()
    {
        var (from, to) = WaveformGeometry.ColumnRange(TimeSpan.Zero, 1, 1f, 0.25f);

        Assert.Equal(400, WaveformGeometry.BucketAt(from, 100));
        Assert.Equal(800, WaveformGeometry.BucketAt(to, 100));
    }

    [Fact]
    public void ColumnRangeCollapsesWhenTheScaleIsDegenerate()
    {
        var clipIn = TimeSpan.FromSeconds(5);

        Assert.Equal((clipIn, clipIn), WaveformGeometry.ColumnRange(clipIn, 7, 1f, 0f));
        Assert.Equal((clipIn, clipIn), WaveformGeometry.ColumnRange(clipIn, 7, 0f, 40f));
    }
}
