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
    public void AddTrack_StopsAtMaxTracks()
    {
        var timeline = new Timeline();

        for (var i = 1; i < Timeline.MaxTracks; i++)
        {
            Assert.NotNull(timeline.AddTrack());
        }

        Assert.Null(timeline.AddTrack());
        Assert.Equal(Timeline.MaxTracks, timeline.Tracks.Count);
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
