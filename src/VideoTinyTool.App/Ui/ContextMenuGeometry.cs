namespace VideoTinyTool.Ui;

public static class ContextMenuGeometry
{
    public const float ScreenMargin = 4f;

    public static float Place(float anchor, float size, float available)
    {
        var start = anchor;
        if (start + size + ScreenMargin > available)
        {
            start = anchor - size;
        }

        var max = Math.Max(ScreenMargin, available - size - ScreenMargin);
        return Math.Clamp(start, ScreenMargin, max);
    }
}
