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

    public static float LaneOffset(float available, int trackCount, int trackIndex, float scroll) =>
        ((trackCount - 1 - trackIndex) * LaneHeight(available, trackCount))
        - ClampScroll(scroll, available, trackCount);

    public static int TrackIndexAt(float available, int trackCount, float scroll, float offsetY)
    {
        if (trackCount <= 0 || offsetY < 0 || offsetY >= available)
        {
            return -1;
        }

        var row = (int)Math.Floor((offsetY + ClampScroll(scroll, available, trackCount))
                                  / LaneHeight(available, trackCount));
        var index = trackCount - 1 - row;

        return index < 0 || index >= trackCount ? -1 : index;
    }

    public static TimeSpan DropStart(TimeSpan pointerTime, TimeSpan grabOffset)
    {
        var start = pointerTime - grabOffset;
        return start < TimeSpan.Zero ? TimeSpan.Zero : start;
    }
}
