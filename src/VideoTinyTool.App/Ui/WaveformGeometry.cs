namespace VideoTinyTool.Ui;

public static class WaveformGeometry
{
    public static int BucketAt(TimeSpan sourceTime, int bucketsPerSecond)
    {
        if (bucketsPerSecond <= 0 || sourceTime <= TimeSpan.Zero)
        {
            return 0;
        }

        var bucket = Math.Floor(sourceTime.TotalSeconds * bucketsPerSecond);
        return bucket >= int.MaxValue ? int.MaxValue : (int)bucket;
    }

    public static byte PeakBetween(ReadOnlySpan<byte> peaks, int firstBucket, int lastBucket)
    {
        if (peaks.IsEmpty || firstBucket > lastBucket)
        {
            return 0;
        }

        var from = Math.Max(0, firstBucket);
        var to = Math.Min(peaks.Length - 1, lastBucket);

        if (from > to)
        {
            return 0;
        }

        byte peak = 0;
        for (var i = from; i <= to; i++)
        {
            if (peaks[i] > peak)
            {
                peak = peaks[i];
            }
        }

        return peak;
    }

    public static (TimeSpan Start, TimeSpan End) ColumnRange(
        TimeSpan clipIn,
        int column,
        float columnWidth,
        float pixelsPerSecond)
    {
        if (pixelsPerSecond <= 0f || columnWidth <= 0f)
        {
            return (clipIn, clipIn);
        }

        var seconds = (double)columnWidth / pixelsPerSecond;
        var start = column * seconds;
        var end = (column + 1) * seconds;

        return (clipIn + TimeSpan.FromSeconds(start), clipIn + TimeSpan.FromSeconds(end));
    }
}
