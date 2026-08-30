using VideoTinyTool.Commands;
using VideoTinyTool.Domain;

namespace VideoTinyTool.Tests.Commands;

public class TrackCommandTests
{
    private static string Snapshot(Timeline timeline) =>
        string.Join(
            ";",
            timeline.Tracks.Select(track =>
                string.Join("|", track.Clips.Select(clip => $"{clip.Id}:{clip.LeadingGap}:{track.StartOf(clip)}"))));

    [Fact]
    public void AddTrackCommand_RoundTrips()
    {
        var timeline = new Timeline();
        var history = new CommandHistory(timeline);
        var command = new AddTrackCommand();

        history.Execute(command);
        Assert.Equal(2, timeline.Tracks.Count);

        history.Undo();
        Assert.Single(timeline.Tracks);

        history.Redo();
        Assert.Equal(2, timeline.Tracks.Count);
        Assert.Same(command.Track, timeline.Tracks[1]);
    }

    [Fact]
    public void AddTrackCommand_IsANoOpAtMaxTracks()
    {
        var timeline = new Timeline();
        var history = new CommandHistory(timeline);

        for (var i = 1; i < Timeline.MaxTracks; i++)
        {
            history.Execute(new AddTrackCommand());
        }

        history.Execute(new AddTrackCommand());
        Assert.Equal(Timeline.MaxTracks, timeline.Tracks.Count);

        history.Undo();
        Assert.Equal(Timeline.MaxTracks, timeline.Tracks.Count);
    }

    [Fact]
    public void RemoveTrackCommand_RestoresTrackWithItsClips()
    {
        var (timeline, sourceId, _, _, _) = TestData.ThreeClipTimeline();
        var history = new CommandHistory(timeline);
        var overlayTrack = timeline.AddTrack()!;
        var first = TestData.Clip(sourceId, 0, 3);
        var second = TestData.Clip(sourceId, 5, 9);
        timeline.Add(1, first);
        timeline.Add(1, second);
        timeline.SetLeadingGap(second, TimeSpan.FromSeconds(2));

        var before = Snapshot(timeline);
        history.Execute(new RemoveTrackCommand(1));

        Assert.Single(timeline.Tracks);
        Assert.Equal(TimeSpan.FromSeconds(12), timeline.TotalDuration);

        history.Undo();

        Assert.Same(overlayTrack, timeline.Tracks[1]);
        Assert.Equal(new[] { first, second }, timeline.ClipsOf(1));
        Assert.Equal(before, Snapshot(timeline));
    }

    [Fact]
    public void RemoveTrackCommand_NeverTargetsTheBaseTrack()
    {
        var (timeline, _, _, _, _) = TestData.ThreeClipTimeline();
        var history = new CommandHistory(timeline);

        history.Execute(new RemoveTrackCommand(0));

        Assert.Single(timeline.Tracks);
        Assert.Equal(3, timeline.Clips.Count);
    }

    [Fact]
    public void SetOverlayTransformCommand_RoundTrips()
    {
        var (timeline, _, a, _, _) = TestData.ThreeClipTimeline();
        var history = new CommandHistory(timeline);
        var target = new OverlayTransform(0.1f, 0.2f, 0.3f, 0.4f, 0.5f);

        history.Execute(new SetOverlayTransformCommand(a, target));
        Assert.Equal(target, a.Overlay);

        history.Undo();
        Assert.Equal(OverlayTransform.Default, a.Overlay);

        history.Redo();
        Assert.Equal(target, a.Overlay);
    }

    [Fact]
    public void MoveClipToTrackCommand_LeavesUntouchedClipsInPlace()
    {
        var (timeline, _, a, b, c) = TestData.ThreeClipTimeline();
        var history = new CommandHistory(timeline);
        timeline.AddTrack();
        timeline.SetLeadingGap(b, TimeSpan.FromSeconds(3));

        var before = Snapshot(timeline);
        history.Execute(new MoveClipToTrackCommand(b, 1, TimeSpan.FromSeconds(5)));

        Assert.Equal(new[] { a, c }, timeline.Clips);
        Assert.Equal(TimeSpan.Zero, timeline.StartOf(a));
        Assert.Equal(TimeSpan.FromSeconds(13), timeline.StartOf(c));
        Assert.Equal(1, timeline.TrackIndexOf(b));
        Assert.Equal(TimeSpan.FromSeconds(5), timeline.StartOf(b));

        history.Undo();
        Assert.Equal(before, Snapshot(timeline));

        history.Redo();
        Assert.Equal(TimeSpan.FromSeconds(5), timeline.StartOf(b));
        Assert.Equal(TimeSpan.FromSeconds(13), timeline.StartOf(c));
    }

    [Fact]
    public void MoveClipToTrackCommand_KeepsTargetTrackClipsInPlace()
    {
        var (timeline, sourceId, a, _, _) = TestData.ThreeClipTimeline();
        var history = new CommandHistory(timeline);
        timeline.AddTrack();
        var resident = TestData.Clip(sourceId, 0, 10);
        timeline.Add(1, resident);
        timeline.SetLeadingGap(resident, TimeSpan.FromSeconds(6));

        var before = Snapshot(timeline);
        history.Execute(new MoveClipToTrackCommand(a, 1, TimeSpan.FromSeconds(1)));

        Assert.Equal(new[] { a, resident }, timeline.ClipsOf(1));
        Assert.Equal(TimeSpan.FromSeconds(1), timeline.StartOf(a));
        Assert.Equal(TimeSpan.FromSeconds(6), timeline.StartOf(resident));

        history.Undo();
        Assert.Equal(before, Snapshot(timeline));
    }

    [Fact]
    public void MoveClipToTrackCommand_AppendsAfterExistingClips()
    {
        var (timeline, sourceId, a, _, _) = TestData.ThreeClipTimeline();
        var history = new CommandHistory(timeline);
        timeline.AddTrack();
        var resident = TestData.Clip(sourceId, 0, 10);
        timeline.Add(1, resident);

        history.Execute(new MoveClipToTrackCommand(a, 1, TimeSpan.FromSeconds(14)));

        Assert.Equal(new[] { resident, a }, timeline.ClipsOf(1));
        Assert.Equal(TimeSpan.FromSeconds(14), timeline.StartOf(a));
        Assert.Equal(TimeSpan.FromSeconds(4), a.LeadingGap);
    }

    [Fact]
    public void EditCommands_WorkOnOverlayClips()
    {
        var (timeline, sourceId, _, _, _) = TestData.ThreeClipTimeline();
        var history = new CommandHistory(timeline);
        timeline.AddTrack();
        var first = TestData.Clip(sourceId, 0, 4);
        var second = TestData.Clip(sourceId, 10, 16);
        timeline.Add(1, first);
        timeline.Add(1, second);
        timeline.SetLeadingGap(second, TimeSpan.FromSeconds(3));

        var before = Snapshot(timeline);
        history.Execute(new RemoveClipCommand(first, false));

        Assert.Equal(new[] { second }, timeline.ClipsOf(1));
        Assert.Equal(TimeSpan.FromSeconds(7), timeline.StartOf(second));

        history.Undo();
        Assert.Equal(before, Snapshot(timeline));

        var split = new SplitClipCommand(second, TimeSpan.FromSeconds(12));
        history.Execute(split);

        Assert.Equal(new[] { first, split.Left, split.Right }, timeline.ClipsOf(1));
        Assert.Equal(TimeSpan.FromSeconds(7), timeline.StartOf(split.Left));

        history.Undo();
        Assert.Equal(before, Snapshot(timeline));

        history.Execute(new ReorderClipCommand(second, 1, 0));
        Assert.Equal(new[] { second, first }, timeline.ClipsOf(1));

        history.Undo();
        Assert.Equal(before, Snapshot(timeline));
    }

    [Fact]
    public void AddClipCommand_TargetsTheRequestedTrack()
    {
        var timeline = new Timeline();
        var history = new CommandHistory(timeline);
        timeline.AddTrack();
        var clip = TestData.Clip(Guid.NewGuid(), 0, 4);

        history.Execute(new AddClipCommand(clip, 0, 1));

        Assert.Empty(timeline.Clips);
        Assert.Equal(new[] { clip }, timeline.ClipsOf(1));

        history.Undo();
        Assert.Empty(timeline.ClipsOf(1));
    }

    [Fact]
    public void RemoveSourceCommand_ClearsEveryTrack()
    {
        var timeline = new Timeline();
        var history = new CommandHistory(timeline);
        var kept = TestData.Source();
        var removed = TestData.Source();
        var sources = new List<MediaSource> { kept, removed };
        timeline.AddTrack();
        timeline.Add(TestData.Clip(kept.Id, 0, 2));
        timeline.Add(TestData.Clip(removed.Id, 0, 3));
        timeline.Add(1, TestData.Clip(removed.Id, 0, 5));
        timeline.Add(1, TestData.Clip(kept.Id, 0, 1));

        var before = Snapshot(timeline);
        history.Execute(new RemoveSourceCommand(
            removed,
            1,
            source => sources.Remove(source),
            (index, source) => sources.Insert(index, source)));

        Assert.Single(timeline.Clips);
        Assert.Single(timeline.ClipsOf(1));
        Assert.DoesNotContain(timeline.ClipsOf(1), clip => clip.SourceId == removed.Id);

        history.Undo();
        Assert.Equal(before, Snapshot(timeline));
        Assert.Equal(new[] { kept, removed }, sources);
    }
}
