using VideoTinyTool.Application;
using VideoTinyTool.Domain;
using VideoTinyTool.Media;

namespace VideoTinyTool.Tests.Media;

public class FrameArgumentBuilderTests
{
    private static ExportSettings Settings(string format = "png", int quality = 4) => new()
    {
        Width = 1920,
        Height = 1080,
        ImageFormat = format,
        ImageQuality = quality
    };

    private static FrameItem Frame(string path, double offsetSeconds) =>
        new(path, TimeSpan.FromSeconds(offsetSeconds));

    private static FrameOverlayItem Overlay(string path, double offsetSeconds, OverlayTransform transform) =>
        new(path, TimeSpan.FromSeconds(offsetSeconds), transform);

    [Fact]
    public void BaseFrame_SeeksTheSourceAndWritesASingleImage()
    {
        var args = FFmpegArgumentBuilder.BuildFrame(
            Frame(@"C:\media\a.mp4", 12.5),
            [],
            Settings(),
            @"C:\out\frame.png");

        Assert.StartsWith("-y -hide_banner", args);
        Assert.Contains(@"-ss 12.5 -i ""C:\media\a.mp4""", args);
        Assert.Contains("-frames:v 1 -update 1", args);
        Assert.Contains("-map \"[v]\"", args);
        Assert.EndsWith(@"""C:\out\frame.png""", args);
    }

    [Fact]
    public void PngIgnoresTheQualitySettingWhileJpgCarriesIt()
    {
        var png = FFmpegArgumentBuilder.BuildFrame(
            Frame(@"C:\media\a.mp4", 1),
            [],
            Settings(),
            @"C:\out\frame.png");

        var jpg = FFmpegArgumentBuilder.BuildFrame(
            Frame(@"C:\media\a.mp4", 1),
            [],
            Settings("jpg", 7),
            @"C:\out\frame.jpg");

        Assert.DoesNotContain("-q:v", png);
        Assert.Contains("-q:v 7", jpg);
    }

    [Fact]
    public void BaseFrame_ScalesAndPadsToTheExportResolution()
    {
        var graph = FFmpegArgumentBuilder.BuildFrameFilterGraph(Frame(@"C:\media\a.mp4", 4), [], Settings());

        Assert.Equal(
            "[0:v]setpts=PTS-STARTPTS,scale=1920:1080:force_original_aspect_ratio=decrease,"
            + "pad=1920:1080:(ow-iw)/2:(oh-ih)/2,setsar=1,format=yuv420p[v]",
            graph);
    }

    [Fact]
    public void AGapUnderThePlayheadBecomesABlackFrameWithoutInputs()
    {
        var args = FFmpegArgumentBuilder.BuildFrame(FrameItem.Gap, [], Settings(), @"C:\out\frame.png");

        Assert.DoesNotContain(" -i ", args);
        Assert.Contains("color=c=black:s=1920x1080:d=1,setsar=1,format=yuv420p[v]", args);
    }

    [Fact]
    public void Overlays_AreSeekedComposedAndMappedFromTheLastStage()
    {
        var args = FFmpegArgumentBuilder.BuildFrame(
            Frame(@"C:\media\a.mp4", 2),
            [
                Overlay(@"C:\media\b.mp4", 8, new OverlayTransform(0.5f, 0.25f, 0.25f, 0.8f)),
                Overlay(@"C:\media\c.mp4", 3, OverlayTransform.Default)
            ],
            Settings(),
            @"C:\out\frame.png");

        Assert.Contains(@"-ss 8 -i ""C:\media\b.mp4""", args);
        Assert.Contains(@"-ss 3 -i ""C:\media\c.mp4""", args);
        Assert.Contains("[1:v]setpts=PTS-STARTPTS,scale=480:-2,setsar=1,format=yuva420p,colorchannelmixer=aa=0.8[ov0]", args);
        Assert.Contains("[v][ov0]overlay=x=960:y=270[acc0]", args);
        Assert.Contains("[acc0][ov1]overlay=", args);
        Assert.Contains("-map \"[acc1]\"", args);
    }

    [Fact]
    public void OverlaysOverAGapKeepTheInputNumberingAtZero()
    {
        var graph = FFmpegArgumentBuilder.BuildFrameFilterGraph(
            FrameItem.Gap,
            [Overlay(@"C:\media\b.mp4", 1, OverlayTransform.Default)],
            Settings());

        Assert.Contains("[0:v]setpts=PTS-STARTPTS,", graph);
    }

    [Fact]
    public void BuildFrameItem_ResolvesTheClipUnderThePlayheadIntoASourceOffset()
    {
        var source = TestData.Source();
        var timeline = new Timeline();
        timeline.Add(TestData.Clip(source.Id, 10, 16));

        var frame = FFmpegArgumentBuilder.BuildFrameItem(
            timeline,
            new Dictionary<Guid, MediaSource> { [source.Id] = source },
            TimeSpan.FromSeconds(2));

        Assert.Equal(source.Path, frame.SourcePath);
        Assert.Equal(TimeSpan.FromSeconds(12), frame.SourceOffset);
    }

    [Fact]
    public void BuildFrameItem_ReturnsAGapPastTheEndOfTheBaseTrack()
    {
        var source = TestData.Source();
        var timeline = new Timeline();
        timeline.Add(TestData.Clip(source.Id, 0, 4));

        var frame = FFmpegArgumentBuilder.BuildFrameItem(
            timeline,
            new Dictionary<Guid, MediaSource> { [source.Id] = source },
            TimeSpan.FromSeconds(9));

        Assert.True(frame.IsGap);
    }

    [Fact]
    public void BuildFrameOverlayItems_TakesOnlyTheVideoTracksAboveTheBaseOne()
    {
        var source = TestData.Source();
        var audioSource = TestData.AudioSource();

        var timeline = new Timeline();
        timeline.Add(TestData.Clip(source.Id, 0, 20));

        var overlayTrack = timeline.AddTrack(TrackKind.Video)!;
        var overlayClip = TestData.Clip(source.Id, 5, 15);
        timeline.Add(timeline.IndexOfTrack(overlayTrack), overlayClip);

        var audioTrack = timeline.AddTrack(TrackKind.Audio)!;
        timeline.Add(timeline.IndexOfTrack(audioTrack), TestData.Clip(audioSource.Id, 0, 20));

        var overlays = FFmpegArgumentBuilder.BuildFrameOverlayItems(
            timeline,
            new Dictionary<Guid, MediaSource> { [source.Id] = source, [audioSource.Id] = audioSource },
            TimeSpan.FromSeconds(3));

        var overlay = Assert.Single(overlays);
        Assert.Equal(source.Path, overlay.SourcePath);
        Assert.Equal(TimeSpan.FromSeconds(8), overlay.SourceOffset);
        Assert.Equal(overlayClip.Overlay, overlay.Transform);
    }
}
