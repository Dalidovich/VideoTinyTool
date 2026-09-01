using VideoTinyTool.Domain;

namespace VideoTinyTool.Tests.Domain;

public class TrackTests
{
    [Fact]
    public void NewTimeline_HasOneBaseTrack()
    {
        var timeline = new Timeline();

        Assert.Single(timeline.Tracks);
        Assert.True(timeline.BaseTrack.IsBase);
        Assert.Same(timeline.BaseTrack.Clips, timeline.Clips);
    }

    [Fact]
    public void AddTrack_StopsAtMaxVideoTracks()
    {
        var timeline = new Timeline();

        for (var i = 1; i < Timeline.MaxVideoTracks; i++)
        {
            Assert.NotNull(timeline.AddTrack());
        }

        Assert.Null(timeline.AddTrack());
        Assert.Equal(Timeline.MaxVideoTracks, timeline.Tracks.Count);
    }

    [Fact]
    public void AudioTracks_AreAppendedAfterEveryVideoTrack()
    {
        var timeline = new Timeline();
        var audio = timeline.AddTrack(TrackKind.Audio)!;

        Assert.Equal(1, timeline.IndexOfTrack(audio));
        Assert.Equal(1, timeline.VideoTrackCount);
        Assert.Equal(1, timeline.AudioTrackCount);
        Assert.Equal(1, timeline.FirstAudioTrackIndex);
        Assert.True(timeline.IsAudioTrack(1));
        Assert.False(timeline.IsAudioTrack(0));

        var video = timeline.AddTrack()!;

        Assert.Equal(1, timeline.IndexOfTrack(video));
        Assert.Equal(2, timeline.IndexOfTrack(audio));
        Assert.Equal(2, timeline.VideoTrackCount);
        Assert.Equal(2, timeline.FirstAudioTrackIndex);
    }

    [Fact]
    public void FirstAudioTrackIndex_IsTheTrackCountWithoutAudioTracks()
    {
        var timeline = new Timeline();
        timeline.AddTrack();

        Assert.Equal(0, timeline.AudioTrackCount);
        Assert.Equal(timeline.Tracks.Count, timeline.FirstAudioTrackIndex);
    }

    [Fact]
    public void PerKindCaps_RefuseIndependently()
    {
        var timeline = new Timeline();

        for (var i = 1; i < Timeline.MaxVideoTracks; i++)
        {
            Assert.NotNull(timeline.AddTrack());
        }

        for (var i = 0; i < Timeline.MaxAudioTracks; i++)
        {
            Assert.NotNull(timeline.AddTrack(TrackKind.Audio));
        }

        Assert.Null(timeline.AddTrack());
        Assert.Null(timeline.AddTrack(TrackKind.Audio));
        Assert.Equal(Timeline.MaxVideoTracks, timeline.VideoTrackCount);
        Assert.Equal(Timeline.MaxAudioTracks, timeline.AudioTrackCount);
    }

    [Fact]
    public void InsertTrack_RefusesAKindLandingInTheWrongRange()
    {
        var timeline = new Timeline();
        timeline.AddTrack();
        timeline.AddTrack(TrackKind.Audio);

        Assert.False(timeline.InsertTrack(3, new Track()));
        Assert.False(timeline.InsertTrack(1, new Track(TrackKind.Audio)));
        Assert.Equal(3, timeline.Tracks.Count);

        Assert.True(timeline.InsertTrack(2, new Track()));
        Assert.True(timeline.InsertTrack(4, new Track(TrackKind.Audio)));
        Assert.Equal(3, timeline.VideoTrackCount);
        Assert.Equal(2, timeline.AudioTrackCount);
    }

    [Fact]
    public void TotalDuration_IsTheLongestTrackWhenItIsAudio()
    {
        var (timeline, sourceId, _, _, _) = TestData.ThreeClipTimeline();
        timeline.AddTrack(TrackKind.Audio);
        timeline.Add(1, TestData.Clip(sourceId, 0, 30));

        Assert.Equal(TimeSpan.FromSeconds(30), timeline.TotalDuration);
    }

    [Fact]
    public void AddedTrack_IsNotBase()
    {
        var timeline = new Timeline();
        var track = timeline.AddTrack();

        Assert.NotNull(track);
        Assert.False(track!.IsBase);
        Assert.Equal(1, timeline.IndexOfTrack(track));
    }

    [Fact]
    public void RemoveTrackAt_RefusesBaseTrack()
    {
        var timeline = new Timeline();
        timeline.AddTrack();

        Assert.False(timeline.RemoveTrackAt(0));
        Assert.Equal(2, timeline.Tracks.Count);
    }

    [Fact]
    public void RemoveTrackAt_DropsOverlayTrack()
    {
        var timeline = new Timeline();
        timeline.AddTrack();

        Assert.True(timeline.RemoveTrackAt(1));
        Assert.Single(timeline.Tracks);
    }

    [Fact]
    public void InsertTrack_RefusesIndexZero()
    {
        var timeline = new Timeline();

        Assert.False(timeline.InsertTrack(0, new Track()));
        Assert.Single(timeline.Tracks);
    }

    [Fact]
    public void TotalDuration_IsTheLongestTrack()
    {
        var (timeline, sourceId, _, _, _) = TestData.ThreeClipTimeline();
        timeline.AddTrack();
        timeline.Add(1, TestData.Clip(sourceId, 0, 30));

        Assert.Equal(TimeSpan.FromSeconds(30), timeline.TotalDuration);
    }

    [Fact]
    public void TotalDuration_KeepsBaseTrackWhenItIsLongest()
    {
        var (timeline, sourceId, _, _, _) = TestData.ThreeClipTimeline();
        timeline.AddTrack();
        timeline.Add(1, TestData.Clip(sourceId, 0, 3));

        Assert.Equal(TimeSpan.FromSeconds(12), timeline.TotalDuration);
    }

    [Fact]
    public void TrackOf_FindsTheOwningTrack()
    {
        var (timeline, sourceId, a, _, _) = TestData.ThreeClipTimeline();
        timeline.AddTrack();
        var overlay = TestData.Clip(sourceId, 0, 5);
        timeline.Add(1, overlay);

        Assert.Same(timeline.BaseTrack, timeline.TrackOf(a));
        Assert.Same(timeline.Tracks[1], timeline.TrackOf(overlay));
        Assert.Equal(1, timeline.TrackIndexOf(overlay));
        Assert.Equal(-1, timeline.TrackIndexOf(TestData.Clip(sourceId, 0, 1)));
    }

    [Fact]
    public void SetBoundsAndGap_WorkOnOverlayClips()
    {
        var timeline = new Timeline();
        var sourceId = Guid.NewGuid();
        timeline.AddTrack();
        var overlay = TestData.Clip(sourceId, 0, 5);
        timeline.Add(1, overlay);

        timeline.SetLeadingGap(overlay, TimeSpan.FromSeconds(2));
        timeline.SetBounds(overlay, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(4));

        Assert.Equal(TimeSpan.FromSeconds(2), timeline.StartOf(overlay));
        Assert.Equal(TimeSpan.FromSeconds(5), timeline.TotalDuration);
        Assert.Same(overlay, timeline.FindById(overlay.Id));
    }

    [Fact]
    public void Resolve_IsScopedToTheRequestedTrack()
    {
        var (timeline, sourceId, _, _, _) = TestData.ThreeClipTimeline();
        timeline.AddTrack();
        var overlay = TestData.Clip(sourceId, 5, 9);
        timeline.Add(1, overlay);
        timeline.SetLeadingGap(overlay, TimeSpan.FromSeconds(2));

        var location = timeline.Resolve(1, TimeSpan.FromSeconds(3));

        Assert.NotNull(location);
        Assert.Same(overlay, location!.Value.Clip);
        Assert.Equal(1, location.Value.TrackIndex);
        Assert.Equal(TimeSpan.FromSeconds(6), location.Value.SourceOffset);
        Assert.Null(timeline.Resolve(1, TimeSpan.FromSeconds(1)));
        Assert.Equal(0, timeline.Resolve(TimeSpan.FromSeconds(3))!.Value.TrackIndex);
    }
}
