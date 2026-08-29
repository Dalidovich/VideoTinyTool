using VideoTinyTool.Application;
using VideoTinyTool.Media;

namespace VideoTinyTool.Tests.Media;

public class FFmpegArgumentBuilderTests
{
    private static ExportSettings Settings() => new()
    {
        Container = "mp4",
        VideoCodec = "libx264",
        Crf = 20,
        Preset = "medium",
        Width = 1920,
        Height = 1080,
        FrameRate = 30,
        AudioCodec = "aac",
        AudioBitrateKbps = 192
    };

    private static ExportItem Item(string path, double inSeconds, double outSeconds, bool hasAudio = true) =>
        new(path, TimeSpan.FromSeconds(inSeconds), TimeSpan.FromSeconds(outSeconds), hasAudio);

    [Fact]
    public void SingleClip_PutsSeekAndDurationBeforeTheInput()
    {
        var args = FFmpegArgumentBuilder.Build(
            [Item(@"C:\media\a.mp4", 2, 5)],
            Settings(),
            @"C:\out\result.mp4");

        Assert.Contains(@"-ss 2 -t 3 -i ""C:\media\a.mp4""", args);
        Assert.StartsWith("-y -hide_banner -stats", args);
        Assert.EndsWith(@"""C:\out\result.mp4""", args);
    }

    [Fact]
    public void SingleClip_CarriesTheEncodingProfileFromSettings()
    {
        var args = FFmpegArgumentBuilder.Build([Item(@"C:\media\a.mp4", 0, 4)], Settings(), @"C:\out\r.mp4");

        Assert.Contains("-c:v libx264", args);
        Assert.Contains("-crf 20", args);
        Assert.Contains("-preset medium", args);
        Assert.Contains("-c:a aac", args);
        Assert.Contains("-b:a 192k", args);
        Assert.Contains("-map \"[v]\" -map \"[a]\"", args);
    }

    [Fact]
    public void SingleClip_ConcatCountIsOne()
    {
        var graph = FFmpegArgumentBuilder.BuildFilterGraph([Item(@"C:\media\a.mp4", 0, 4)], Settings());

        Assert.Contains("[v0][a0]concat=n=1:v=1:a=1[v][a]", graph);
    }

    [Fact]
    public void ThreeClips_ProduceThreeInputsAndConcatOfThree()
    {
        var items = new[]
        {
            Item(@"C:\media\a.mp4", 0, 4),
            Item(@"C:\media\b.mp4", 10, 16),
            Item(@"C:\media\c.mp4", 20, 22)
        };

        var args = FFmpegArgumentBuilder.Build(items, Settings(), @"C:\out\r.mp4");
        var graph = FFmpegArgumentBuilder.BuildFilterGraph(items, Settings());

        Assert.Equal(3, args.Split(" -i ").Length - 1);
        Assert.Contains("[v0][a0][v1][a1][v2][a2]concat=n=3:v=1:a=1[v][a]", graph);
    }

    [Fact]
    public void EveryClip_IsScaledPaddedAndNormalisedToTheTargetProfile()
    {
        var graph = FFmpegArgumentBuilder.BuildFilterGraph([Item(@"C:\media\a.mp4", 0, 4)], Settings());

        Assert.Contains("scale=1920:1080:force_original_aspect_ratio=decrease", graph);
        Assert.Contains("pad=1920:1080:(ow-iw)/2:(oh-ih)/2", graph);
        Assert.Contains("setsar=1,fps=30,format=yuv420p[v0]", graph);
        Assert.Contains("[0:a]aresample=48000,aformat=sample_fmts=fltp:channel_layouts=stereo[a0]", graph);
    }

    [Fact]
    public void ClipWithoutAudio_GetsSilenceOfItsOwnDuration()
    {
        var graph = FFmpegArgumentBuilder.BuildFilterGraph(
            [Item(@"C:\media\silent.mp4", 1, 4.5, hasAudio: false)],
            Settings());

        Assert.Contains("anullsrc=channel_layout=stereo:sample_rate=48000:d=3.5", graph);
        Assert.DoesNotContain("[0:a]", graph);
    }

    [Fact]
    public void MixedClips_OnlyTheSilentOneGetsAnullsrc()
    {
        var graph = FFmpegArgumentBuilder.BuildFilterGraph(
            [
                Item(@"C:\media\a.mp4", 0, 2),
                Item(@"C:\media\silent.mp4", 0, 2, hasAudio: false),
                Item(@"C:\media\c.mp4", 0, 2)
            ],
            Settings());

        Assert.Contains("[0:a]aresample", graph);
        Assert.Contains("[2:a]aresample", graph);
        Assert.DoesNotContain("[1:a]aresample", graph);
        Assert.Equal(1, graph.Split("anullsrc").Length - 1);
        Assert.Contains("[v0][a0][v1][a1][v2][a2]concat=n=3:v=1:a=1[v][a]", graph);
    }

    [Fact]
    public void EmptyTimeline_IsRejected()
    {
        Assert.Throws<ArgumentException>(() =>
            FFmpegArgumentBuilder.Build([], Settings(), @"C:\out\r.mp4"));
    }

    [Fact]
    public void FractionalTimes_UseInvariantFormatting()
    {
        var args = FFmpegArgumentBuilder.Build(
            [Item(@"C:\media\a.mp4", 1.25, 3.5)],
            Settings(),
            @"C:\out\r.mp4");

        Assert.Contains("-ss 1.25 -t 2.25", args);
    }
}
