using VideoTinyTool.Domain;

namespace VideoTinyTool.Tests;

internal static class TestData
{
    public static MediaSource Source(
        double durationSeconds = 60,
        double frameRate = 25,
        bool hasAudio = true,
        int width = 1920,
        int height = 1080) =>
        new(
            Guid.NewGuid(),
            @"C:\media\clip.mp4",
            TimeSpan.FromSeconds(durationSeconds),
            width,
            height,
            frameRate,
            "h264",
            hasAudio ? "aac" : null);

    public static Clip Clip(Guid sourceId, double inSeconds, double outSeconds) =>
        VideoTinyTool.Domain.Clip.Create(sourceId, TimeSpan.FromSeconds(inSeconds), TimeSpan.FromSeconds(outSeconds));

    public static (Timeline Timeline, Guid SourceId, Clip A, Clip B, Clip C) ThreeClipTimeline()
    {
        var timeline = new Timeline();
        var sourceId = Guid.NewGuid();

        var a = Clip(sourceId, 0, 4);
        var b = Clip(sourceId, 10, 16);
        var c = Clip(sourceId, 20, 22);

        timeline.Add(a);
        timeline.Add(b);
        timeline.Add(c);

        return (timeline, sourceId, a, b, c);
    }
}
