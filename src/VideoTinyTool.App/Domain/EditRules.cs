namespace VideoTinyTool.Domain;

public static class EditRules
{
    public static readonly TimeSpan FallbackMinimumDuration = TimeSpan.FromMilliseconds(40);

    public static TimeSpan MinimumDuration(MediaSource? source) =>
        source?.FrameDuration ?? FallbackMinimumDuration;

    public static bool TryTrimIn(Clip clip, TimeSpan desiredIn, TimeSpan minimum, out TimeSpan result)
    {
        result = clip.In;

        var candidate = desiredIn < TimeSpan.Zero ? TimeSpan.Zero : desiredIn;
        if (clip.Out - candidate < minimum)
        {
            return false;
        }

        result = candidate;
        return result != clip.In;
    }

    public static bool TryTrimOut(
        Clip clip,
        TimeSpan desiredOut,
        TimeSpan minimum,
        TimeSpan sourceDuration,
        out TimeSpan result)
    {
        result = clip.Out;

        var candidate = desiredOut > sourceDuration ? sourceDuration : desiredOut;
        if (candidate - clip.In < minimum)
        {
            return false;
        }

        result = candidate;
        return result != clip.Out;
    }

    public static bool CanSplit(Clip clip, TimeSpan offsetInClip, TimeSpan minimum) =>
        offsetInClip >= minimum && clip.Duration - offsetInClip >= minimum;
}
