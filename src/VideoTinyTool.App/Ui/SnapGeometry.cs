namespace VideoTinyTool.Ui;

public static class SnapGeometry
{
    public const float RadiusPixels = 10f;

    public static bool TrySnap(
        TimeSpan value,
        IReadOnlyList<TimeSpan> targets,
        float pixelsPerSecond,
        out TimeSpan snapped)
    {
        snapped = value;

        if (targets.Count == 0 || pixelsPerSecond <= 0f)
        {
            return false;
        }

        var best = double.MaxValue;

        foreach (var target in targets)
        {
            var distance = Math.Abs((target - value).TotalSeconds) * pixelsPerSecond;

            if (distance <= RadiusPixels && distance < best)
            {
                best = distance;
                snapped = target;
            }
        }

        return best < double.MaxValue;
    }

    public static TimeSpan Snap(TimeSpan value, IReadOnlyList<TimeSpan> targets, float pixelsPerSecond) =>
        TrySnap(value, targets, pixelsPerSecond, out var snapped) ? snapped : value;

    public static TimeSpan SnapSpan(
        TimeSpan start,
        TimeSpan duration,
        IReadOnlyList<TimeSpan> targets,
        float pixelsPerSecond)
    {
        if (targets.Count == 0 || pixelsPerSecond <= 0f)
        {
            return start;
        }

        var result = start;
        var best = double.MaxValue;

        foreach (var target in targets)
        {
            Consider(target);
            Consider(target - duration);
        }

        return result;

        void Consider(TimeSpan candidate)
        {
            if (candidate < TimeSpan.Zero)
            {
                return;
            }

            var distance = Math.Abs((candidate - start).TotalSeconds) * pixelsPerSecond;

            if (distance <= RadiusPixels && distance < best)
            {
                best = distance;
                result = candidate;
            }
        }
    }
}
