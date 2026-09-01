namespace VideoTinyTool.Ui;

public static class LaneGeometry
{
    public const float MinLaneHeight = 44f;

    public static float LaneHeight(float available, int trackCount)
    {
        if (trackCount <= 1)
        {
            return Math.Max(MinLaneHeight, available);
        }

        return Math.Max(MinLaneHeight, available / trackCount);
    }

    public static float StackHeight(float available, int trackCount) =>
        LaneHeight(available, trackCount) * Math.Max(1, trackCount);

    public static float MaxScroll(float available, int trackCount) =>
        Math.Max(0f, StackHeight(available, trackCount) - available);

    public static float ClampScroll(float scroll, float available, int trackCount) =>
        Math.Clamp(scroll, 0f, MaxScroll(available, trackCount));

    public static int RowOf(int videoCount, int trackCount, int trackIndex)
    {
        if (trackIndex < 0 || trackIndex >= trackCount)
        {
            return -1;
        }

        var video = Math.Clamp(videoCount, 0, trackCount);
        return trackIndex < video ? video - 1 - trackIndex : trackIndex;
    }

    public static int TrackIndexAtRow(int videoCount, int trackCount, int row)
    {
        if (row < 0 || row >= trackCount)
        {
            return -1;
        }

        var video = Math.Clamp(videoCount, 0, trackCount);
        return row < video ? video - 1 - row : row;
    }

    public static float LaneOffset(float available, int videoCount, int trackCount, int trackIndex, float scroll) =>
        (RowOf(videoCount, trackCount, trackIndex) * LaneHeight(available, trackCount))
        - ClampScroll(scroll, available, trackCount);

    public static int TrackIndexAt(float available, int videoCount, int trackCount, float scroll, float offsetY)
    {
        if (trackCount <= 0 || offsetY < 0 || offsetY >= available)
        {
            return -1;
        }

        var row = (int)Math.Floor((offsetY + ClampScroll(scroll, available, trackCount))
                                  / LaneHeight(available, trackCount));

        return TrackIndexAtRow(videoCount, trackCount, row);
    }

    public static TimeSpan DropStart(TimeSpan pointerTime, TimeSpan grabOffset)
    {
        var start = pointerTime - grabOffset;
        return start < TimeSpan.Zero ? TimeSpan.Zero : start;
    }
}
