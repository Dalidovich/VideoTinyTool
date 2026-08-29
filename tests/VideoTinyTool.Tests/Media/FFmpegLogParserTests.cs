using VideoTinyTool.Media;

namespace VideoTinyTool.Tests.Media;

public class FFmpegLogParserTests
{
    [Fact]
    public void ParsesATypicalStatsLine()
    {
        const string line =
            "frame=  312 fps= 31 q=28.0 size=    1024kB time=00:00:12.34 bitrate= 679.7kbits/s speed=1.23x";

        Assert.True(FFmpegLogParser.TryParseTime(line, out var time));
        Assert.Equal(12.34, time.TotalSeconds, 3);
    }

    [Fact]
    public void ParsesHoursMinutesAndSeconds()
    {
        Assert.True(FFmpegLogParser.TryParseTime("time=01:02:03.500 bitrate=1", out var time));
        Assert.Equal(new TimeSpan(0, 1, 2, 3, 500), time);
    }

    [Fact]
    public void ParsesATimeWithoutFractionalSeconds()
    {
        Assert.True(FFmpegLogParser.TryParseTime("time=00:00:07 q=-1.0", out var time));
        Assert.Equal(TimeSpan.FromSeconds(7), time);
    }

    [Fact]
    public void ParsesATimeWithSpacesAfterTheEquals()
    {
        Assert.True(FFmpegLogParser.TryParseTime("time= 00:00:02.00", out var time));
        Assert.Equal(TimeSpan.FromSeconds(2), time);
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("Press [q] to stop, [?] for help")]
    [InlineData("  Stream #0:0(und): Video: h264 (avc1 / 0x31637661), yuv420p, 1920x1080")]
    [InlineData("time=N/A bitrate=N/A")]
    [InlineData("Conversion failed!")]
    public void IgnoresLinesWithoutAUsableTime(string? line)
    {
        Assert.False(FFmpegLogParser.TryParseTime(line, out var time));
        Assert.Equal(TimeSpan.Zero, time);
    }

    [Fact]
    public void IgnoresTheNegativeStartTimeFfmpegPrintsWhileSeeking()
    {
        Assert.False(FFmpegLogParser.TryParseTime("time=-00:00:00.02 bitrate=N/A", out _));
    }

    [Fact]
    public void IgnoresAMalformedClockValue()
    {
        Assert.False(FFmpegLogParser.TryParseTime("time=00:99:00.00", out _));
    }
}
