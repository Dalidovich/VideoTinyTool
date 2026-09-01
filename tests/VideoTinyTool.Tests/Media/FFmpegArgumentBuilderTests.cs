using System.Globalization;
using VideoTinyTool.Application;
using VideoTinyTool.Domain;
using VideoTinyTool.Media;

namespace VideoTinyTool.Tests.Media;

public class FFmpegArgumentBuilderTests
{
    private static ExportSettings Settings(double speed = ExportSettings.NormalSpeed) => new()
    {
        Container = "mp4",
        VideoCodec = "libx264",
        Crf = 20,
        Preset = "medium",
        Width = 1920,
        Height = 1080,
        FrameRate = 30,
        Speed = speed,
        AudioCodec = "aac",
        AudioBitrateKbps = 192
    };

    private static ExportItem Item(string path, double inSeconds, double outSeconds, bool hasAudio = true, float gain = 1f) =>
        new(path, TimeSpan.FromSeconds(inSeconds), TimeSpan.FromSeconds(outSeconds), hasAudio, gain);

    private static AudioItem Audio(string path, double inSeconds, double outSeconds, double startSeconds, float gain = 1f) =>
        new(
            path,
            TimeSpan.FromSeconds(inSeconds),
            TimeSpan.FromSeconds(outSeconds),
            TimeSpan.FromSeconds(startSeconds),
            gain);

    private static OverlayItem Overlay(
        string path,
        double inSeconds,
        double outSeconds,
        double startSeconds,
        OverlayTransform transform,
        bool hasAudio = false,
        float volume = 1f) =>
        new(
            path,
            TimeSpan.FromSeconds(inSeconds),
            TimeSpan.FromSeconds(outSeconds),
            hasAudio,
            TimeSpan.FromSeconds(startSeconds),
            transform,
            new ClipAudio(volume, false));

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
    public void Gap_BecomesBlackVideoAndSilenceOfItsOwnDuration()
    {
        var graph = FFmpegArgumentBuilder.BuildFilterGraph(
            [ExportItem.Gap(TimeSpan.FromSeconds(2.5)), Item(@"C:\media\a.mp4", 0, 4)],
            Settings());

        Assert.Contains("color=c=black:s=1920x1080:r=30:d=2.5,setsar=1,format=yuv420p[v0]", graph);
        Assert.Contains("anullsrc=channel_layout=stereo:sample_rate=48000:d=2.5", graph);
        Assert.Contains("[v0][a0][v1][a1]concat=n=2:v=1:a=1[v][a]", graph);
    }

    [Fact]
    public void Gap_DoesNotConsumeAnInputStream()
    {
        var items = new[]
        {
            ExportItem.Gap(TimeSpan.FromSeconds(1)),
            Item(@"C:\media\a.mp4", 0, 4),
            ExportItem.Gap(TimeSpan.FromSeconds(2)),
            Item(@"C:\media\b.mp4", 0, 4)
        };

        var args = FFmpegArgumentBuilder.Build(items, Settings(), @"C:\out\r.mp4");
        var graph = FFmpegArgumentBuilder.BuildFilterGraph(items, Settings());

        Assert.Equal(2, args.Split(" -i ").Length - 1);
        Assert.Contains("[0:v]scale=", graph);
        Assert.Contains("[1:v]scale=", graph);
        Assert.DoesNotContain("[2:v]", graph);
        Assert.Contains("[0:a]aresample", graph);
        Assert.Contains("[1:a]aresample", graph);
        Assert.Contains("[v0][a0][v1][a1][v2][a2][v3][a3]concat=n=4:v=1:a=1[v][a]", graph);
    }

    [Fact]
    public void BuildItems_TurnsALeadingGapIntoItsOwnItem()
    {
        var source = TestData.Source();
        var timeline = new Timeline();
        var first = TestData.Clip(source.Id, 0, 4);
        var second = TestData.Clip(source.Id, 10, 16);

        timeline.Add(first);
        timeline.Add(second);
        timeline.SetLeadingGap(second, TimeSpan.FromSeconds(3));

        var items = FFmpegArgumentBuilder.BuildItems(timeline, new Dictionary<Guid, MediaSource> { [source.Id] = source });

        Assert.Equal(3, items.Count);
        Assert.False(items[0].IsGap);
        Assert.True(items[1].IsGap);
        Assert.Equal(TimeSpan.FromSeconds(3), items[1].Duration);
        Assert.False(items[2].IsGap);
        Assert.Equal(source.Path, items[2].SourcePath);
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

    [Fact]
    public void NoOverlays_LeavesTheBaseGraphByteIdentical()
    {
        var items = new[]
        {
            Item(@"C:\media\a.mp4", 0, 4),
            ExportItem.Gap(TimeSpan.FromSeconds(2)),
            Item(@"C:\media\b.mp4", 1, 3, hasAudio: false)
        };

        var graph = FFmpegArgumentBuilder.BuildFilterGraph(items, [], Settings());

        Assert.Equal(
            "[0:v]scale=1920:1080:force_original_aspect_ratio=decrease," +
            "pad=1920:1080:(ow-iw)/2:(oh-ih)/2,setsar=1,fps=30,format=yuv420p[v0];" +
            "[0:a]aresample=48000,aformat=sample_fmts=fltp:channel_layouts=stereo[a0];" +
            "color=c=black:s=1920x1080:r=30:d=2,setsar=1,format=yuv420p[v1];" +
            "anullsrc=channel_layout=stereo:sample_rate=48000:d=2," +
            "aformat=sample_fmts=fltp:channel_layouts=stereo[a1];" +
            "[1:v]scale=1920:1080:force_original_aspect_ratio=decrease," +
            "pad=1920:1080:(ow-iw)/2:(oh-ih)/2,setsar=1,fps=30,format=yuv420p[v2];" +
            "anullsrc=channel_layout=stereo:sample_rate=48000:d=2," +
            "aformat=sample_fmts=fltp:channel_layouts=stereo[a2];" +
            "[v0][a0][v1][a1][v2][a2]concat=n=3:v=1:a=1[v][a]",
            graph);

        Assert.Equal(FFmpegArgumentBuilder.BuildFilterGraph(items, Settings()), graph);
    }

    [Fact]
    public void SingleOverlay_ScalesShiftsAndCompositesOntoTheBaseVideo()
    {
        var graph = FFmpegArgumentBuilder.BuildFilterGraph(
            [Item(@"C:\media\a.mp4", 0, 10)],
            [Overlay(@"C:\media\pip.mp4", 0, 3, 2, OverlayTransform.Default)],
            Settings());

        Assert.EndsWith(
            "[v0][a0]concat=n=1:v=1:a=1[v][a];" +
            "[1:v]scale=614:-2,setsar=1,fps=30,format=yuva420p,colorchannelmixer=aa=1," +
            "setpts=PTS-STARTPTS+2/TB[ov0];" +
            "[v][ov0]overlay=x=1190:y=65:eof_action=pass:shortest=0[acc0]",
            graph);
    }

    [Fact]
    public void SingleOverlay_IsInputAfterEveryBaseInputAndBecomesTheMappedVideo()
    {
        var args = FFmpegArgumentBuilder.Build(
            [Item(@"C:\media\a.mp4", 0, 10), ExportItem.Gap(TimeSpan.FromSeconds(1)), Item(@"C:\media\b.mp4", 0, 5)],
            [Overlay(@"C:\media\pip.mp4", 1, 4, 2, OverlayTransform.Default)],
            Settings(),
            @"C:\out\r.mp4");

        Assert.Equal(3, args.Split(" -i ").Length - 1);
        Assert.Contains(@"-ss 0 -t 5 -i ""C:\media\b.mp4"" -ss 1 -t 3 -i ""C:\media\pip.mp4""", args);
        Assert.Contains("[2:v]scale=614:-2", args);
        Assert.Contains("-map \"[acc0]\" -map \"[a]\"", args);
    }

    [Fact]
    public void TwoOverlays_ChainInTrackOrder()
    {
        var graph = FFmpegArgumentBuilder.BuildFilterGraph(
            [Item(@"C:\media\a.mp4", 0, 10)],
            [
                Overlay(@"C:\media\one.mp4", 0, 3, 0, new OverlayTransform(0f, 0f, 0.25f, 1f)),
                Overlay(@"C:\media\two.mp4", 0, 3, 4, new OverlayTransform(0.5f, 0.5f, 0.5f, 0.5f))
            ],
            Settings());

        Assert.EndsWith(
            "[1:v]scale=480:-2,setsar=1,fps=30,format=yuva420p,colorchannelmixer=aa=1," +
            "setpts=PTS-STARTPTS+0/TB[ov0];" +
            "[v][ov0]overlay=x=0:y=0:eof_action=pass:shortest=0[acc0];" +
            "[2:v]scale=960:-2,setsar=1,fps=30,format=yuva420p,colorchannelmixer=aa=0.5," +
            "setpts=PTS-STARTPTS+4/TB[ov1];" +
            "[acc0][ov1]overlay=x=960:y=540:eof_action=pass:shortest=0[acc1]",
            graph);
    }

    [Fact]
    public void OverlayWithAudio_IsDelayedAndMixedIntoTheBaseAudio()
    {
        var overlays = new[]
        {
            Overlay(@"C:\media\pip.mp4", 0, 3, 1.25, new OverlayTransform(0f, 0f, 0.25f, 1f), hasAudio: true, volume: 0.5f)
        };

        var graph = FFmpegArgumentBuilder.BuildFilterGraph([Item(@"C:\media\a.mp4", 0, 10)], overlays, Settings());
        var args = FFmpegArgumentBuilder.Build([Item(@"C:\media\a.mp4", 0, 10)], overlays, Settings(), @"C:\out\r.mp4");

        Assert.EndsWith(
            "[1:a]aresample=48000,aformat=sample_fmts=fltp:channel_layouts=stereo," +
            "volume=0.5,adelay=1250|1250[oa0];" +
            "[a][oa0]amix=inputs=2:normalize=0:dropout_transition=0[amixed]",
            graph);
        Assert.Contains("-map \"[acc0]\" -map \"[amixed]\"", args);
    }

    [Fact]
    public void TwoOverlaysWithAudio_MixThreeInputs()
    {
        var graph = FFmpegArgumentBuilder.BuildFilterGraph(
            [Item(@"C:\media\a.mp4", 0, 10)],
            [
                Overlay(@"C:\media\one.mp4", 0, 3, 0, OverlayTransform.Default, hasAudio: true),
                Overlay(@"C:\media\two.mp4", 0, 3, 2, OverlayTransform.Default, hasAudio: true)
            ],
            Settings());

        Assert.Contains("adelay=0|0[oa0]", graph);
        Assert.Contains("adelay=2000|2000[oa1]", graph);
        Assert.EndsWith("[a][oa0][oa1]amix=inputs=3:normalize=0:dropout_transition=0[amixed]", graph);
    }

    [Fact]
    public void MutedOverlay_ContributesNoAudio()
    {
        var overlays = new[]
        {
            Overlay(@"C:\media\pip.mp4", 0, 3, 1, new OverlayTransform(0f, 0f, 0.25f, 1f), hasAudio: true, volume: 0f)
        };

        var graph = FFmpegArgumentBuilder.BuildFilterGraph([Item(@"C:\media\a.mp4", 0, 10)], overlays, Settings());
        var args = FFmpegArgumentBuilder.Build([Item(@"C:\media\a.mp4", 0, 10)], overlays, Settings(), @"C:\out\r.mp4");

        Assert.DoesNotContain("amix", graph);
        Assert.DoesNotContain("[oa", graph);
        Assert.Contains("-map \"[acc0]\" -map \"[a]\"", args);
    }

    [Fact]
    public void OverlayWithoutAudioTrack_ContributesNoAudio()
    {
        var graph = FFmpegArgumentBuilder.BuildFilterGraph(
            [Item(@"C:\media\a.mp4", 0, 10)],
            [Overlay(@"C:\media\pip.mp4", 0, 3, 1, OverlayTransform.Default)],
            Settings());

        Assert.DoesNotContain("amix", graph);
        Assert.DoesNotContain("[oa", graph);
        Assert.DoesNotContain("[1:a]", graph);
    }

    [Fact]
    public void OverlayNumbers_UseInvariantFormattingUnderACommaDecimalCulture()
    {
        var previous = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = new CultureInfo("ru-RU");

        try
        {
            var graph = FFmpegArgumentBuilder.BuildFilterGraph(
                [Item(@"C:\media\a.mp4", 0, 10)],
                [Overlay(@"C:\media\pip.mp4", 0, 3, 1.5, new OverlayTransform(0f, 0f, 0.25f, 0.5f), hasAudio: true, volume: 0.25f)],
                Settings());

            Assert.Contains("scale=480:-2", graph);
            Assert.Contains("colorchannelmixer=aa=0.5", graph);
            Assert.Contains("setpts=PTS-STARTPTS+1.5/TB", graph);
            Assert.Contains("volume=0.25,adelay=1500|1500[oa0]", graph);
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }

    [Fact]
    public void NormalSpeed_LeavesTheGraphAndTheMapsUntouched()
    {
        var items = new[] { Item(@"C:\media\a.mp4", 0, 4) };

        var graph = FFmpegArgumentBuilder.BuildFilterGraph(items, [], Settings());
        var args = FFmpegArgumentBuilder.Build(items, Settings(), @"C:\out\r.mp4");

        Assert.DoesNotContain("setpts=PTS/", graph);
        Assert.DoesNotContain("atempo", graph);
        Assert.Contains("-map \"[v]\" -map \"[a]\"", args);
    }

    [Fact]
    public void FasterSpeed_RetimesTheConcatenatedVideoAndAudio()
    {
        var items = new[] { Item(@"C:\media\a.mp4", 0, 4) };

        var graph = FFmpegArgumentBuilder.BuildFilterGraph(items, [], Settings(1.5));
        var args = FFmpegArgumentBuilder.Build(items, Settings(1.5), @"C:\out\r.mp4");

        Assert.EndsWith(
            "concat=n=1:v=1:a=1[v][a];[v]setpts=PTS/1.5,fps=30[vspeed];[a]atempo=1.5[aspeed]",
            graph);
        Assert.Contains("-map \"[vspeed]\" -map \"[aspeed]\"", args);
    }

    [Fact]
    public void SlowerSpeed_StretchesThePictureAndTheSound()
    {
        var graph = FFmpegArgumentBuilder.BuildFilterGraph([Item(@"C:\media\a.mp4", 0, 4)], [], Settings(0.5));

        Assert.EndsWith("[v]setpts=PTS/0.5,fps=30[vspeed];[a]atempo=0.5[aspeed]", graph);
    }

    [Fact]
    public void SpeedAboveTwo_ChainsTwoTempoStages()
    {
        var graph = FFmpegArgumentBuilder.BuildFilterGraph([Item(@"C:\media\a.mp4", 0, 4)], [], Settings(3));

        Assert.EndsWith("[v]setpts=PTS/3,fps=30[vspeed];[a]atempo=2,atempo=1.5[aspeed]", graph);
    }

    [Fact]
    public void Speed_RetimesTheCompositeAfterOverlaysAndMixing()
    {
        var items = new[] { Item(@"C:\media\a.mp4", 0, 10) };
        var overlays = new[] { Overlay(@"C:\media\pip.mp4", 0, 3, 1, OverlayTransform.Default, hasAudio: true) };

        var graph = FFmpegArgumentBuilder.BuildFilterGraph(items, overlays, Settings(2));
        var args = FFmpegArgumentBuilder.Build(items, overlays, Settings(2), @"C:\out\r.mp4");

        Assert.EndsWith("[acc0]setpts=PTS/2,fps=30[vspeed];[amixed]atempo=2[aspeed]", graph);
        Assert.Contains("-map \"[vspeed]\" -map \"[aspeed]\"", args);
    }

    [Fact]
    public void SpeedNumbers_UseInvariantFormattingUnderACommaDecimalCulture()
    {
        var previous = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = new CultureInfo("ru-RU");

        try
        {
            var graph = FFmpegArgumentBuilder.BuildFilterGraph([Item(@"C:\media\a.mp4", 0, 10)], [], Settings(2.5));

            Assert.Contains("setpts=PTS/2.5", graph);
            Assert.Contains("atempo=2,atempo=1.25", graph);
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }

    [Theory]
    [InlineData(1.0, 60)]
    [InlineData(2.0, 30)]
    [InlineData(0.5, 120)]
    [InlineData(0, 60)]
    [InlineData(9, 20)]
    public void OutputDuration_ScalesTheTimelineByTheClampedSpeed(double speed, double expected)
    {
        var duration = FFmpegArgumentBuilder.OutputDuration(TimeSpan.FromSeconds(60), Settings(speed));

        Assert.Equal(TimeSpan.FromSeconds(expected), duration);
    }

    [Fact]
    public void BuildOverlayItems_SkipsTheBaseTrackAndOrdersByTrackThenStart()
    {
        var source = TestData.Source();
        var sources = new Dictionary<Guid, MediaSource> { [source.Id] = source };

        var timeline = new Timeline();
        timeline.Add(TestData.Clip(source.Id, 0, 10));

        timeline.AddTrack();
        timeline.AddTrack();

        var first = TestData.Clip(source.Id, 0, 2);
        var second = TestData.Clip(source.Id, 5, 8);
        timeline.Add(1, first);
        timeline.Add(1, second);
        timeline.SetLeadingGap(first, TimeSpan.FromSeconds(1));

        var third = TestData.Clip(source.Id, 20, 24);
        timeline.Add(2, third);
        timeline.SetLeadingGap(third, TimeSpan.FromSeconds(4));
        timeline.SetOverlay(third, new OverlayTransform(0.1f, 0.2f, 0.3f, 0.4f));
        timeline.SetClipAudio(third, new ClipAudio(0.5f, false));

        var overlays = FFmpegArgumentBuilder.BuildOverlayItems(timeline, sources);

        Assert.Equal(3, overlays.Count);
        Assert.Equal(TimeSpan.FromSeconds(1), overlays[0].Start);
        Assert.Equal(TimeSpan.FromSeconds(3), overlays[1].Start);
        Assert.Equal(TimeSpan.FromSeconds(4), overlays[2].Start);
        Assert.Equal(TimeSpan.FromSeconds(20), overlays[2].In);
        Assert.Equal(new OverlayTransform(0.1f, 0.2f, 0.3f, 0.4f), overlays[2].Transform);
        Assert.Equal(new ClipAudio(0.5f, false), overlays[2].Audio);
        Assert.True(overlays[0].HasAudio);
    }

    [Fact]
    public void BuildOverlayItems_SkipsClipsWithoutASourceButKeepsLaterPositions()
    {
        var source = TestData.Source();
        var sources = new Dictionary<Guid, MediaSource> { [source.Id] = source };

        var timeline = new Timeline();
        timeline.Add(TestData.Clip(source.Id, 0, 10));
        timeline.AddTrack();

        timeline.Add(1, TestData.Clip(Guid.NewGuid(), 0, 3));
        timeline.Add(1, TestData.Clip(source.Id, 0, 2));

        var overlays = FFmpegArgumentBuilder.BuildOverlayItems(timeline, sources);

        Assert.Single(overlays);
        Assert.Equal(TimeSpan.FromSeconds(3), overlays[0].Start);
    }

    [Fact]
    public void NoAudioItemsAndFullGain_LeavesTheBaseGraphByteIdentical()
    {
        var items = new[]
        {
            Item(@"C:\media\a.mp4", 0, 4),
            ExportItem.Gap(TimeSpan.FromSeconds(2)),
            Item(@"C:\media\b.mp4", 1, 3, hasAudio: false)
        };

        var graph = FFmpegArgumentBuilder.BuildFilterGraph(items, [], [], Settings());

        Assert.Equal(
            "[0:v]scale=1920:1080:force_original_aspect_ratio=decrease," +
            "pad=1920:1080:(ow-iw)/2:(oh-ih)/2,setsar=1,fps=30,format=yuv420p[v0];" +
            "[0:a]aresample=48000,aformat=sample_fmts=fltp:channel_layouts=stereo[a0];" +
            "color=c=black:s=1920x1080:r=30:d=2,setsar=1,format=yuv420p[v1];" +
            "anullsrc=channel_layout=stereo:sample_rate=48000:d=2," +
            "aformat=sample_fmts=fltp:channel_layouts=stereo[a1];" +
            "[1:v]scale=1920:1080:force_original_aspect_ratio=decrease," +
            "pad=1920:1080:(ow-iw)/2:(oh-ih)/2,setsar=1,fps=30,format=yuv420p[v2];" +
            "anullsrc=channel_layout=stereo:sample_rate=48000:d=2," +
            "aformat=sample_fmts=fltp:channel_layouts=stereo[a2];" +
            "[v0][a0][v1][a1][v2][a2]concat=n=3:v=1:a=1[v][a]",
            graph);

        Assert.Equal(FFmpegArgumentBuilder.BuildFilterGraph(items, Settings()), graph);
    }

    [Fact]
    public void QuietBaseClip_GetsAVolumeFilterOnItsOwnAudioChainOnly()
    {
        var items = new[]
        {
            Item(@"C:\media\a.mp4", 0, 4),
            Item(@"C:\media\b.mp4", 0, 4, gain: 0.5f)
        };

        var graph = FFmpegArgumentBuilder.BuildFilterGraph(items, [], [], Settings());

        Assert.Contains("[0:a]aresample=48000,aformat=sample_fmts=fltp:channel_layouts=stereo[a0];", graph);
        Assert.Contains(
            "[1:a]aresample=48000,aformat=sample_fmts=fltp:channel_layouts=stereo,volume=0.5[a1];",
            graph);
        Assert.Equal(1, graph.Split("volume=").Length - 1);
        Assert.Contains("[v0][a0][v1][a1]concat=n=2:v=1:a=1[v][a]", graph);
    }

    [Fact]
    public void MutedBaseClip_BecomesSilenceOfItsOwnDuration()
    {
        var items = new[]
        {
            Item(@"C:\media\a.mp4", 0, 4),
            Item(@"C:\media\b.mp4", 1, 3.5, gain: 0f)
        };

        var graph = FFmpegArgumentBuilder.BuildFilterGraph(items, [], [], Settings());

        Assert.Contains(
            "anullsrc=channel_layout=stereo:sample_rate=48000:d=2.5," +
            "aformat=sample_fmts=fltp:channel_layouts=stereo[a1];",
            graph);
        Assert.DoesNotContain("volume=", graph);
        Assert.DoesNotContain("[1:a]", graph);
        Assert.Contains("[v0][a0][v1][a1]concat=n=2:v=1:a=1[v][a]", graph);
    }

    [Fact]
    public void AudioItem_IsInputAfterEveryOverlayAndIsDelayedIntoTheMix()
    {
        var items = new[] { Item(@"C:\media\a.mp4", 0, 20) };
        var overlays = new[] { Overlay(@"C:\media\pip.mp4", 0, 3, 2, OverlayTransform.Default) };
        var audio = new[] { Audio(@"C:\media\track.mp3", 5, 15, 1.5) };

        var graph = FFmpegArgumentBuilder.BuildFilterGraph(items, overlays, audio, Settings());
        var args = FFmpegArgumentBuilder.Build(items, overlays, audio, Settings(), @"C:\out\r.mp4");

        Assert.EndsWith(
            "[2:a]aresample=48000,aformat=sample_fmts=fltp:channel_layouts=stereo," +
            "volume=1,adelay=1500|1500[aa0];" +
            "[a][aa0]amix=inputs=2:normalize=0:dropout_transition=0[amixed]",
            graph);
        Assert.Contains(@"-i ""C:\media\pip.mp4"" -ss 5 -t 10 -i ""C:\media\track.mp3""", args);
        Assert.Contains("-map \"[acc0]\" -map \"[amixed]\"", args);
    }

    [Fact]
    public void OverlaySoundAndTwoAudioTracks_MixFourInputsInTrackThenStartOrder()
    {
        var video = TestData.Source();
        var music = TestData.AudioSource();
        var sources = new Dictionary<Guid, MediaSource> { [video.Id] = video, [music.Id] = music };

        var timeline = new Timeline();
        timeline.Add(TestData.Clip(video.Id, 0, 20));

        timeline.AddTrack(TrackKind.Audio);
        timeline.AddTrack(TrackKind.Audio);

        var first = TestData.Clip(music.Id, 0, 4);
        var second = TestData.Clip(music.Id, 10, 12);
        timeline.Add(1, first);
        timeline.Add(2, second);
        timeline.SetLeadingGap(first, TimeSpan.FromSeconds(2));
        timeline.SetLeadingGap(second, TimeSpan.FromSeconds(0.5));
        timeline.SetClipAudio(second, new ClipAudio(0.25f, false));

        var audio = FFmpegArgumentBuilder.BuildAudioItems(timeline, sources);

        var items = new[] { Item(@"C:\media\a.mp4", 0, 20) };
        var overlays = new[]
        {
            Overlay(@"C:\media\pip.mp4", 0, 3, 1, OverlayTransform.Default, hasAudio: true)
        };

        var graph = FFmpegArgumentBuilder.BuildFilterGraph(items, overlays, audio, Settings());

        Assert.Equal(2, audio.Count);
        Assert.Contains("volume=1,adelay=2000|2000[aa0]", graph);
        Assert.Contains("volume=0.25,adelay=500|500[aa1]", graph);
        Assert.EndsWith("[a][oa0][aa0][aa1]amix=inputs=4:normalize=0:dropout_transition=0[amixed]", graph);
    }

    [Fact]
    public void MutedAudioTrack_ContributesNothingToTheMix()
    {
        var video = TestData.Source();
        var music = TestData.AudioSource();
        var sources = new Dictionary<Guid, MediaSource> { [video.Id] = video, [music.Id] = music };

        var timeline = new Timeline();
        timeline.Add(TestData.Clip(video.Id, 0, 20));
        timeline.AddTrack(TrackKind.Audio);

        var clip = TestData.Clip(music.Id, 0, 4);
        timeline.Add(1, clip);
        timeline.SetClipAudio(clip, new ClipAudio(1f, true));

        var audio = FFmpegArgumentBuilder.BuildAudioItems(timeline, sources);

        var items = new[] { Item(@"C:\media\a.mp4", 0, 20) };
        var graph = FFmpegArgumentBuilder.BuildFilterGraph(items, [], audio, Settings());
        var args = FFmpegArgumentBuilder.Build(items, [], audio, Settings(), @"C:\out\r.mp4");

        Assert.Empty(audio);
        Assert.DoesNotContain("[aa", graph);
        Assert.DoesNotContain("amix", graph);
        Assert.Contains("-map \"[v]\" -map \"[a]\"", args);
    }

    [Fact]
    public void BuildAudioItems_SkipsSilentSourcesButKeepsLaterPositions()
    {
        var video = TestData.Source();
        var silent = TestData.Source(hasAudio: false);
        var music = TestData.AudioSource();
        var sources = new Dictionary<Guid, MediaSource>
        {
            [video.Id] = video,
            [silent.Id] = silent,
            [music.Id] = music
        };

        var timeline = new Timeline();
        timeline.Add(TestData.Clip(video.Id, 0, 20));
        timeline.AddTrack(TrackKind.Audio);

        timeline.Add(1, TestData.Clip(Guid.NewGuid(), 0, 3));
        timeline.Add(1, TestData.Clip(silent.Id, 0, 2));
        timeline.Add(1, TestData.Clip(music.Id, 4, 9));

        var audio = FFmpegArgumentBuilder.BuildAudioItems(timeline, sources);

        Assert.Single(audio);
        Assert.Equal(music.Path, audio[0].SourcePath);
        Assert.Equal(TimeSpan.FromSeconds(5), audio[0].Start);
        Assert.Equal(TimeSpan.FromSeconds(5), audio[0].Duration);
    }

    private static ExportSettings AudioSettings(string container = "mp3", double speed = ExportSettings.NormalSpeed)
    {
        var settings = Settings(speed);
        settings.AudioContainer = container;
        return settings;
    }

    [Fact]
    public void AudioOnly_WritesEveryTrackAsAnInputAndMapsTheMix()
    {
        var args = FFmpegArgumentBuilder.BuildAudioOnly(
            [Audio(@"C:\media\track.mp3", 0.5, 4.25, 2.5)],
            AudioSettings(),
            @"C:\out\result.mp3",
            TimeSpan.FromSeconds(10));

        Assert.Contains(@"-ss 0.5 -t 3.75 -i ""C:\media\track.mp3""", args);
        Assert.Contains(@"-f lavfi -i ""anullsrc=channel_layout=stereo:sample_rate=48000:d=10""", args);
        Assert.Contains(
            "volume=1,adelay=2500|2500[aa0];[1:a][aa0]amix=inputs=2:normalize=0:dropout_transition=0[aout]",
            args);
        Assert.Contains(@"-map ""[aout]""", args);
        Assert.Contains("-c:a libmp3lame", args);
        Assert.Contains("-b:a 192k", args);
        Assert.EndsWith(@"""C:\out\result.mp3""", args);
    }

    [Fact]
    public void AudioOnly_CarriesNoVideoStream()
    {
        var args = FFmpegArgumentBuilder.BuildAudioOnly(
            [Audio(@"C:\media\track.mp3", 0, 4, 0)],
            AudioSettings(),
            @"C:\out\result.mp3",
            TimeSpan.FromSeconds(4));

        Assert.Contains(" -vn", args);
        Assert.DoesNotContain("-c:v", args);
        Assert.DoesNotContain("[v]", args);
    }

    [Fact]
    public void AudioOnly_UsesTheEncoderThatMatchesTheContainer()
    {
        var args = FFmpegArgumentBuilder.BuildAudioOnly(
            [Audio(@"C:\media\track.mp3", 0, 4, 0)],
            AudioSettings("m4a"),
            @"C:\out\result.m4a",
            TimeSpan.FromSeconds(4));

        Assert.Contains("-c:a aac", args);
    }

    [Fact]
    public void AudioOnly_AppliesSpeedWithATempoAndMapsTheSpedUpLabel()
    {
        var args = FFmpegArgumentBuilder.BuildAudioOnly(
            [Audio(@"C:\media\track.mp3", 0, 8, 0)],
            AudioSettings(speed: 1.5),
            @"C:\out\result.mp3",
            TimeSpan.FromSeconds(8));

        Assert.Contains("[aout]atempo=1.5[aspeed]", args);
        Assert.Contains(@"-map ""[aspeed]""", args);
    }

    [Fact]
    public void AudioOnly_AboveDoubleSpeed_ChainsTwoTempoSteps()
    {
        var args = FFmpegArgumentBuilder.BuildAudioOnly(
            [Audio(@"C:\media\track.mp3", 0, 8, 0)],
            AudioSettings(speed: 3),
            @"C:\out\result.mp3",
            TimeSpan.FromSeconds(8));

        Assert.Contains("[aout]atempo=2,atempo=1.5[aspeed]", args);
    }

    [Fact]
    public void AudioOnly_WithoutAudibleTracks_StillRendersSilenceForTheWholeTimeline()
    {
        var args = FFmpegArgumentBuilder.BuildAudioOnly(
            [],
            AudioSettings(),
            @"C:\out\result.mp3",
            TimeSpan.FromSeconds(12));

        Assert.Contains(@"-f lavfi -i ""anullsrc=channel_layout=stereo:sample_rate=48000:d=12""", args);
        Assert.Contains("[0:a]amix=inputs=1:normalize=0:dropout_transition=0[aout]", args);
    }

    [Fact]
    public void AudioOnly_DelaysEveryTrackToItsOwnPlaceOnTheTimeline()
    {
        var args = FFmpegArgumentBuilder.BuildAudioOnly(
            [
                Audio(@"C:\media\one.mp3", 0, 4, 0),
                Audio(@"C:\media\two.mp3", 1, 3, 6, 0.5f)
            ],
            AudioSettings(),
            @"C:\out\result.mp3",
            TimeSpan.FromSeconds(9));

        Assert.Contains("adelay=0|0[aa0]", args);
        Assert.Contains("volume=0.5,adelay=6000|6000[aa1]", args);
        Assert.Contains("[2:a][aa0][aa1]amix=inputs=3", args);
    }

    [Fact]
    public void AudioNumbers_UseInvariantFormattingUnderACommaDecimalCulture()
    {
        var previous = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = new CultureInfo("ru-RU");

        try
        {
            var args = FFmpegArgumentBuilder.Build(
                [Item(@"C:\media\a.mp4", 0, 10, gain: 0.75f)],
                [],
                [Audio(@"C:\media\track.mp3", 0.5, 4.25, 2.5, 0.125f)],
                Settings(),
                @"C:\out\r.mp4");

            Assert.Contains("channel_layouts=stereo,volume=0.75[a0]", args);
            Assert.Contains(@"-ss 0.5 -t 3.75 -i ""C:\media\track.mp3""", args);
            Assert.Contains("volume=0.125,adelay=2500|2500[aa0]", args);
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }
}
