using VideoTinyTool.Commands;
using VideoTinyTool.Domain;

namespace VideoTinyTool.Tests.Commands;

public class AudioCommandTests
{
    private static string Snapshot(Timeline timeline) =>
        string.Join(
            ";",
            timeline.Tracks.Select(track =>
                $"{track.Kind}:" + string.Join(
                    "|",
                    track.Clips.Select(clip => $"{clip.Id}:{clip.LeadingGap}:{track.StartOf(clip)}:{clip.Audio}"))));

    [Fact]
    public void SetClipAudioCommand_RoundTrips()
    {
        var (timeline, _, a, _, _) = TestData.ThreeClipTimeline();
        var history = new CommandHistory(timeline);
        var target = new ClipAudio(0.4f, false);

        history.Execute(new SetClipAudioCommand(a, target));
        Assert.Equal(target, a.Audio);

        history.Undo();
        Assert.Equal(ClipAudio.Default, a.Audio);

        history.Redo();
        Assert.Equal(target, a.Audio);
    }

    [Fact]
    public void DetachAudioCommand_MutesTheClipAndMirrorsItOnANewAudioTrack()
    {
        var source = TestData.Source();
        var timeline = new Timeline();
        var history = new CommandHistory(timeline);
        var first = TestData.Clip(source.Id, 0, 4);
        var second = TestData.Clip(source.Id, 10, 16);
        timeline.Add(first);
        timeline.Add(second);
        timeline.SetClipAudio(second, new ClipAudio(0.5f, false));

        var before = Snapshot(timeline);
        var command = new DetachAudioCommand(second, source);
        history.Execute(command);

        Assert.Equal(1, timeline.AudioTrackCount);
        Assert.Equal(1, timeline.FirstAudioTrackIndex);
        Assert.Equal(new ClipAudio(0.5f, true), second.Audio);

        var detached = Assert.Single(timeline.ClipsOf(1));
        Assert.Same(command.Detached, detached);
        Assert.Equal(source.Id, detached.SourceId);
        Assert.Equal(second.In, detached.In);
        Assert.Equal(second.Out, detached.Out);
        Assert.Equal(new ClipAudio(0.5f, false), detached.Audio);
        Assert.Equal(timeline.StartOf(second), timeline.StartOf(detached));

        history.Undo();
        Assert.Equal(before, Snapshot(timeline));
        Assert.Single(timeline.Tracks);

        history.Redo();
        Assert.Equal(1, timeline.AudioTrackCount);
        Assert.Equal(timeline.StartOf(second), timeline.StartOf(timeline.ClipsOf(1)[0]));
    }

    [Fact]
    public void DetachAudioCommand_ReusesAFreeAudioTrackAndLeavesItsClipsInPlace()
    {
        var source = TestData.Source();
        var timeline = new Timeline();
        var history = new CommandHistory(timeline);
        var first = TestData.Clip(source.Id, 0, 4);
        var second = TestData.Clip(source.Id, 10, 16);
        timeline.Add(first);
        timeline.Add(second);

        timeline.AddTrack(TrackKind.Audio);
        var resident = TestData.Clip(source.Id, 0, 2);
        var follower = TestData.Clip(source.Id, 0, 2);
        timeline.Add(1, resident);
        timeline.Add(1, follower);
        timeline.SetLeadingGap(follower, TimeSpan.FromSeconds(18));

        var before = Snapshot(timeline);
        history.Execute(new DetachAudioCommand(second, source));

        Assert.Single(timeline.Tracks, track => track.IsAudio);
        Assert.Equal(3, timeline.ClipsOf(1).Count);

        var detached = timeline.ClipsOf(1)[1];
        Assert.Equal(TimeSpan.FromSeconds(4), timeline.StartOf(detached));
        Assert.Equal(TimeSpan.FromSeconds(2), detached.LeadingGap);
        Assert.Equal(TimeSpan.FromSeconds(20), timeline.StartOf(follower));
        Assert.Equal(TimeSpan.Zero, timeline.StartOf(resident));

        history.Undo();
        Assert.Equal(before, Snapshot(timeline));
    }

    [Fact]
    public void DetachAudioCommand_AddsASecondTrackWhenTheFirstIsBusy()
    {
        var source = TestData.Source();
        var timeline = new Timeline();
        var history = new CommandHistory(timeline);
        var clip = TestData.Clip(source.Id, 0, 4);
        timeline.Add(clip);

        timeline.AddTrack(TrackKind.Audio);
        timeline.Add(1, TestData.Clip(source.Id, 0, 10));

        history.Execute(new DetachAudioCommand(clip, source));

        Assert.Equal(2, timeline.AudioTrackCount);
        Assert.Single(timeline.ClipsOf(2));
        Assert.Equal(TimeSpan.Zero, timeline.StartOf(timeline.ClipsOf(2)[0]));

        history.Undo();
        Assert.Equal(1, timeline.AudioTrackCount);
        Assert.Single(timeline.ClipsOf(1));
        Assert.Equal(ClipAudio.Default, clip.Audio);
    }

    [Fact]
    public void DetachAudioCommand_IsANoOpForASourceWithoutAudio()
    {
        var source = TestData.Source(hasAudio: false);
        var timeline = new Timeline();
        var history = new CommandHistory(timeline);
        var clip = TestData.Clip(source.Id, 0, 4);
        timeline.Add(clip);

        var before = Snapshot(timeline);
        history.Execute(new DetachAudioCommand(clip, source));

        Assert.Equal(before, Snapshot(timeline));
        Assert.Single(timeline.Tracks);

        history.Undo();
        Assert.Equal(before, Snapshot(timeline));
    }

    [Fact]
    public void DetachAudioCommand_IsANoOpForAClipAlreadyOnAnAudioTrack()
    {
        var source = TestData.Source();
        var timeline = new Timeline();
        var history = new CommandHistory(timeline);
        timeline.AddTrack(TrackKind.Audio);
        var clip = TestData.Clip(source.Id, 0, 4);
        timeline.Add(1, clip);

        var before = Snapshot(timeline);
        history.Execute(new DetachAudioCommand(clip, source));

        Assert.Equal(before, Snapshot(timeline));
        Assert.Equal(1, timeline.AudioTrackCount);
    }

    [Fact]
    public void DetachAudioCommand_IsANoOpWhenEveryAudioTrackIsBusyAndCapped()
    {
        var source = TestData.Source();
        var timeline = new Timeline();
        var history = new CommandHistory(timeline);
        var clip = TestData.Clip(source.Id, 0, 4);
        timeline.Add(clip);

        for (var i = 0; i < Timeline.MaxAudioTracks; i++)
        {
            timeline.AddTrack(TrackKind.Audio);
            timeline.Add(1 + i, TestData.Clip(source.Id, 0, 10));
        }

        var before = Snapshot(timeline);
        history.Execute(new DetachAudioCommand(clip, source));

        Assert.Equal(before, Snapshot(timeline));
        Assert.Equal(ClipAudio.Default, clip.Audio);
    }

    [Fact]
    public void RemoveSourceCommand_ClearsAudioTracksToo()
    {
        var timeline = new Timeline();
        var history = new CommandHistory(timeline);
        var kept = TestData.Source();
        var removed = TestData.Source();
        var sources = new List<MediaSource> { kept, removed };
        timeline.AddTrack(TrackKind.Audio);
        timeline.Add(TestData.Clip(kept.Id, 0, 2));
        timeline.Add(1, TestData.Clip(removed.Id, 0, 5));
        var survivor = TestData.Clip(kept.Id, 0, 1);
        timeline.Add(1, survivor);
        timeline.SetLeadingGap(survivor, TimeSpan.FromSeconds(3));

        var before = Snapshot(timeline);
        history.Execute(new RemoveSourceCommand(
            removed,
            1,
            source => sources.Remove(source),
            (index, source) => sources.Insert(index, source)));

        Assert.Equal(new[] { survivor }, timeline.ClipsOf(1));
        Assert.Equal(TimeSpan.FromSeconds(8), timeline.StartOf(survivor));

        history.Undo();
        Assert.Equal(before, Snapshot(timeline));
        Assert.Equal(new[] { kept, removed }, sources);
    }
}
