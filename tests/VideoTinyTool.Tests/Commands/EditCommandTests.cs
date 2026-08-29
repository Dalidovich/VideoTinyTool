using VideoTinyTool.Commands;
using VideoTinyTool.Domain;

namespace VideoTinyTool.Tests.Commands;

public class EditCommandTests
{
    private static string Snapshot(Timeline timeline) =>
        string.Join("|", timeline.Clips.Select(clip => $"{clip.Id}:{clip.In}:{clip.Out}"));

    [Fact]
    public void AddClip_ExecuteThenUndo_RestoresTheTimeline()
    {
        var (timeline, sourceId, _, _, _) = TestData.ThreeClipTimeline();
        var before = Snapshot(timeline);
        var command = new AddClipCommand(TestData.Clip(sourceId, 0, 3), 1);

        command.Execute(timeline);
        Assert.Equal(4, timeline.Clips.Count);

        command.Undo(timeline);
        Assert.Equal(before, Snapshot(timeline));
    }

    [Fact]
    public void AddClip_InsertsAtTheRequestedIndex()
    {
        var (timeline, sourceId, a, _, _) = TestData.ThreeClipTimeline();
        var added = TestData.Clip(sourceId, 0, 3);

        new AddClipCommand(added, 0).Execute(timeline);

        Assert.Same(added, timeline.Clips[0]);
        Assert.Same(a, timeline.Clips[1]);
    }

    [Fact]
    public void RemoveClip_ExecuteThenUndo_RestoresPositionAndOrder()
    {
        var (timeline, _, _, b, _) = TestData.ThreeClipTimeline();
        var before = Snapshot(timeline);
        var command = new RemoveClipCommand(b);

        command.Execute(timeline);
        Assert.Equal(2, timeline.Clips.Count);
        Assert.DoesNotContain(b, timeline.Clips);

        command.Undo(timeline);
        Assert.Equal(before, Snapshot(timeline));
        Assert.Equal(1, timeline.IndexOf(b));
    }

    [Fact]
    public void TrimClip_ExecuteThenUndo_RestoresTheOriginalBounds()
    {
        var (timeline, _, _, b, _) = TestData.ThreeClipTimeline();
        var before = Snapshot(timeline);
        var command = new TrimClipCommand(b, TimeSpan.FromSeconds(11), TimeSpan.FromSeconds(15));

        command.Execute(timeline);
        Assert.Equal(TimeSpan.FromSeconds(11), b.In);
        Assert.Equal(TimeSpan.FromSeconds(15), b.Out);
        Assert.Equal(TimeSpan.FromSeconds(10), timeline.TotalDuration);

        command.Undo(timeline);
        Assert.Equal(before, Snapshot(timeline));
    }

    [Fact]
    public void SplitClip_HalvesAddUpToTheOriginalDuration()
    {
        var (timeline, _, _, b, _) = TestData.ThreeClipTimeline();
        var originalDuration = b.Duration;
        var command = new SplitClipCommand(b, TimeSpan.FromSeconds(13));

        command.Execute(timeline);

        Assert.Equal(4, timeline.Clips.Count);
        Assert.Same(command.Left, timeline.Clips[1]);
        Assert.Same(command.Right, timeline.Clips[2]);
        Assert.Equal(originalDuration, command.Left.Duration + command.Right.Duration);
    }

    [Fact]
    public void SplitClip_CutsAtTheRequestedPointInTheSource()
    {
        var (timeline, _, _, b, _) = TestData.ThreeClipTimeline();
        var command = new SplitClipCommand(b, TimeSpan.FromSeconds(13));

        command.Execute(timeline);

        Assert.Equal(TimeSpan.FromSeconds(10), command.Left.In);
        Assert.Equal(TimeSpan.FromSeconds(13), command.Left.Out);
        Assert.Equal(TimeSpan.FromSeconds(13), command.Right.In);
        Assert.Equal(TimeSpan.FromSeconds(16), command.Right.Out);
    }

    [Fact]
    public void SplitClip_ExecuteThenUndo_RestoresTheTimeline()
    {
        var (timeline, _, _, b, _) = TestData.ThreeClipTimeline();
        var before = Snapshot(timeline);
        var command = new SplitClipCommand(b, TimeSpan.FromSeconds(13));

        command.Execute(timeline);
        command.Undo(timeline);

        Assert.Equal(before, Snapshot(timeline));
        Assert.Same(b, timeline.Clips[1]);
    }

    [Fact]
    public void ReorderClip_MovesTheLastClipToTheFrontAndBack()
    {
        var (timeline, _, a, b, c) = TestData.ThreeClipTimeline();
        var command = new ReorderClipCommand(c, 2, 0);

        command.Execute(timeline);
        Assert.Equal(new[] { c, a, b }, timeline.Clips);

        command.Undo(timeline);
        Assert.Equal(new[] { a, b, c }, timeline.Clips);
    }

    [Fact]
    public void ReorderClip_MovesTheFirstClipToTheEndAndBack()
    {
        var (timeline, _, a, b, c) = TestData.ThreeClipTimeline();
        var command = new ReorderClipCommand(a, 0, 2);

        command.Execute(timeline);
        Assert.Equal(new[] { b, c, a }, timeline.Clips);

        command.Undo(timeline);
        Assert.Equal(new[] { a, b, c }, timeline.Clips);
    }

    [Fact]
    public void ReorderClip_KeepsTotalDurationUnchanged()
    {
        var (timeline, _, _, _, c) = TestData.ThreeClipTimeline();
        var before = timeline.TotalDuration;

        new ReorderClipCommand(c, 2, 1).Execute(timeline);

        Assert.Equal(before, timeline.TotalDuration);
    }
}
