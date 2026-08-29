using System.Globalization;
using System.Text.RegularExpressions;

namespace VideoTinyTool.Media;

public static partial class FFmpegLogParser
{
    [GeneratedRegex(@"time=\s*(-?)(\d+):(\d{1,2}):(\d{1,2}(?:\.\d+)?)", RegexOptions.CultureInvariant)]
    private static partial Regex TimePattern();

    public static bool TryParseTime(string? line, out TimeSpan time)
    {
        time = TimeSpan.Zero;
        if (string.IsNullOrEmpty(line))
        {
            return false;
        }

        var match = TimePattern().Match(line);
        if (!match.Success)
        {
            return false;
        }

        if (match.Groups[1].Value == "-")
        {
            return false;
        }

        var hours = int.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture);
        var minutes = int.Parse(match.Groups[3].Value, CultureInfo.InvariantCulture);
        var seconds = double.Parse(match.Groups[4].Value, CultureInfo.InvariantCulture);

        if (minutes > 59 || seconds >= 60)
        {
            return false;
        }

        time = TimeSpan.FromHours(hours) + TimeSpan.FromMinutes(minutes) + TimeSpan.FromSeconds(seconds);
        return true;
    }
}
