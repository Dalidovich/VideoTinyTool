using System.Globalization;

namespace VideoTinyTool.Ui;

public static class TimeFormat
{
    public static string Timecode(TimeSpan value)
    {
        if (value < TimeSpan.Zero)
        {
            value = TimeSpan.Zero;
        }

        return value.TotalHours >= 1
            ? string.Format(
                CultureInfo.InvariantCulture,
                "{0:0}:{1:00}:{2:00}.{3:000}",
                (int)value.TotalHours,
                value.Minutes,
                value.Seconds,
                value.Milliseconds)
            : string.Format(
                CultureInfo.InvariantCulture,
                "{0:00}:{1:00}.{2:000}",
                (int)value.TotalMinutes,
                value.Seconds,
                value.Milliseconds);
    }

    public static string Short(TimeSpan value)
    {
        if (value < TimeSpan.Zero)
        {
            value = TimeSpan.Zero;
        }

        return value.TotalHours >= 1
            ? string.Format(
                CultureInfo.InvariantCulture,
                "{0:0}:{1:00}:{2:00}",
                (int)value.TotalHours,
                value.Minutes,
                value.Seconds)
            : string.Format(
                CultureInfo.InvariantCulture,
                "{0:00}:{1:00}",
                (int)value.TotalMinutes,
                value.Seconds);
    }

    public static string Seconds(TimeSpan value) =>
        value.TotalSeconds.ToString("0.000", CultureInfo.InvariantCulture);

    public static string FrameRate(double value) =>
        Math.Abs(value - Math.Round(value)) < 0.01
            ? ((int)Math.Round(value)).ToString(CultureInfo.InvariantCulture)
            : value.ToString("0.##", CultureInfo.InvariantCulture);
}
