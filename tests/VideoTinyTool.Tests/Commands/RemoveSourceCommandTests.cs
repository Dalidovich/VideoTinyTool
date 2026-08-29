using VideoTinyTool.Commands;
using VideoTinyTool.Domain;

namespace VideoTinyTool.Tests.Commands;

public class RemoveSourceCommandTests
{
    private static string Snapshot(Timeline timeline) =>
        string.Join("|", timeline.Clips.Select(clip => $"{clip.SourceId}:{clip.In}:{clip.Out}"));

    private static (Timeline Timeline, List<MediaSource> Sources, MediaSource Removed) MixedTimeline()
    {
        var kept = TestData.Source();
        var removed = TestData.Source();
        var timeline = new Timeline();

        timeline.Add(TestData.Clip(kept.Id, 0, 2));
        timeline.Add(TestData.Clip(removed.Id, 0, 3));
        timeline.Add(TestData.Clip(kept.Id, 5, 8));
        timeline.Add(TestData.Clip(removed.Id, 10, 12));

        return (timeline, new List<MediaSource> { kept, removed }, removed);
    }

    private static RemoveSourceCommand Command(List<MediaSource> sources, MediaSource source) =>
        new(
            source,
            sources.IndexOf(source),
            removed => sources.Remove(removed),
            (index, restored) => sources.Insert(index, restored));

    [Fact]
    public void Execute_RemovesTheSourceAndEveryClipThatUsesIt()
    {
        var (timeline, sources, removed) = MixedTimeline();

        Command(sources, removed).Execute(timeline);

        Assert.Equal(2, timeline.Clips.Count);
        Assert.DoesNotContain(timeline.Clips, clip => clip.SourceId == removed.Id);
        Assert.DoesNotContain(removed, sources);
    }

    [Fact]
    public void Undo_RestoresTheClipsAtTheirOriginalPositions()
    {
        var (timeline, sources, removed) = MixedTimeline();
        var before = Snapshot(timeline);
        var command = Command(sources, removed);

        command.Execute(timeline);
        command.Undo(timeline);

        Assert.Equal(before, Snapshot(timeline));
        Assert.Equal(1, sources.IndexOf(removed));
    }

    [Fact]
    public void Execute_UnusedSource_LeavesTheTimelineAlone()
    {
        var sources = new List<MediaSource> { TestData.Source(), TestData.Source() };
        var timeline = new Timeline();
        timeline.Add(TestData.Clip(sources[0].Id, 0, 2));
        var before = Snapshot(timeline);

        Command(sources, sources[1]).Execute(timeline);

        Assert.Equal(before, Snapshot(timeline));
        Assert.Single(sources);
    }
}
