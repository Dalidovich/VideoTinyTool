using VideoTinyTool.Domain;

namespace VideoTinyTool.Tests.Domain;

public class TimelineTests
{
    [Fact]
    public void TotalDuration_IsTheSumOfClipDurations()
    {
        var (timeline, _, _, _, _) = TestData.ThreeClipTimeline();

        Assert.Equal(TimeSpan.FromSeconds(12), timeline.TotalDuration);
    }

    [Fact]
    public void TotalDuration_OfEmptyTimeline_IsZero()
    {
        Assert.Equal(TimeSpan.Zero, new Timeline().TotalDuration);
    }

    [Fact]
    public void Resolve_AtTheVeryStart_ReturnsFirstClip()
    {
        var (timeline, _, a, _, _) = TestData.ThreeClipTimeline();

        var location = timeline.Resolve(TimeSpan.Zero);

        Assert.NotNull(location);
        Assert.Same(a, location!.Value.Clip);
        Assert.Equal(0, location.Value.Index);
        Assert.Equal(a.In, location.Value.SourceOffset);
    }

    [Fact]
    public void Resolve_InsideTheMiddleClip_MapsToSourceOffset()
    {
        var (timeline, _, _, b, _) = TestData.ThreeClipTimeline();

        var location = timeline.Resolve(TimeSpan.FromSeconds(6));

        Assert.NotNull(location);
        Assert.Same(b, location!.Value.Clip);
        Assert.Equal(1, location.Value.Index);
        Assert.Equal(TimeSpan.FromSeconds(12), location.Value.SourceOffset);
    }

    [Fact]
    public void Resolve_ExactlyOnABoundary_BelongsToTheNextClip()
    {
        var (timeline, _, _, b, _) = TestData.ThreeClipTimeline();

        var location = timeline.Resolve(TimeSpan.FromSeconds(4));

        Assert.NotNull(location);
        Assert.Same(b, location!.Value.Clip);
        Assert.Equal(b.In, location.Value.SourceOffset);
    }

    [Fact]
    public void Resolve_AtTotalDuration_ReturnsNull()
    {
        var (timeline, _, _, _, _) = TestData.ThreeClipTimeline();

        Assert.Null(timeline.Resolve(timeline.TotalDuration));
    }

    [Fact]
    public void Resolve_BeforeZero_ReturnsNull()
    {
        var (timeline, _, _, _, _) = TestData.ThreeClipTimeline();

        Assert.Null(timeline.Resolve(TimeSpan.FromSeconds(-0.5)));
    }

    [Fact]
    public void StartOf_ReturnsTheGlobalStartOfEachClip()
    {
        var (timeline, _, a, b, c) = TestData.ThreeClipTimeline();

        Assert.Equal(TimeSpan.Zero, timeline.StartOf(a));
        Assert.Equal(TimeSpan.FromSeconds(4), timeline.StartOf(b));
        Assert.Equal(TimeSpan.FromSeconds(10), timeline.StartOf(c));
    }

    [Fact]
    public void Move_ShiftsTheClipAndTheStartsFollow()
    {
        var (timeline, _, a, b, c) = TestData.ThreeClipTimeline();

        timeline.Move(2, 0);

        Assert.Equal(new[] { c, a, b }, timeline.Clips);
        Assert.Equal(TimeSpan.Zero, timeline.StartOf(c));
        Assert.Equal(TimeSpan.FromSeconds(2), timeline.StartOf(a));
    }

    [Fact]
    public void TotalDuration_CountsLeadingGaps()
    {
        var (timeline, _, _, b, _) = TestData.ThreeClipTimeline();

        timeline.SetLeadingGap(b, TimeSpan.FromSeconds(3));

        Assert.Equal(TimeSpan.FromSeconds(15), timeline.TotalDuration);
    }

    [Fact]
    public void StartOf_CountsTheLeadingGap()
    {
        var (timeline, _, a, b, c) = TestData.ThreeClipTimeline();

        timeline.SetLeadingGap(b, TimeSpan.FromSeconds(3));

        Assert.Equal(TimeSpan.Zero, timeline.StartOf(a));
        Assert.Equal(TimeSpan.FromSeconds(7), timeline.StartOf(b));
        Assert.Equal(TimeSpan.FromSeconds(13), timeline.StartOf(c));
    }

    [Fact]
    public void StartOf_ByIndex_MatchesStartOfByClip()
    {
        var (timeline, _, _, b, _) = TestData.ThreeClipTimeline();

        timeline.SetLeadingGap(b, TimeSpan.FromSeconds(3));

        Assert.Equal(timeline.StartOf(b), timeline.StartOf(1));
    }

    [Fact]
    public void StartOf_PastTheLastIndex_IsTheTotalDuration()
    {
        var (timeline, _, _, _, _) = TestData.ThreeClipTimeline();

        Assert.Equal(timeline.TotalDuration, timeline.StartOf(timeline.Clips.Count));
    }

    [Fact]
    public void Resolve_InsideAGap_ReturnsNull()
    {
        var (timeline, _, _, b, _) = TestData.ThreeClipTimeline();

        timeline.SetLeadingGap(b, TimeSpan.FromSeconds(3));

        Assert.Null(timeline.Resolve(TimeSpan.FromSeconds(5)));
    }

    [Fact]
    public void Resolve_AfterAGap_MapsToTheClipThatFollowsIt()
    {
        var (timeline, _, _, b, _) = TestData.ThreeClipTimeline();

        timeline.SetLeadingGap(b, TimeSpan.FromSeconds(3));
        var location = timeline.Resolve(TimeSpan.FromSeconds(9));

        Assert.NotNull(location);
        Assert.Same(b, location!.Value.Clip);
        Assert.Equal(TimeSpan.FromSeconds(12), location.Value.SourceOffset);
    }

    [Fact]
    public void NextClipStart_InsideAGap_IsTheStartOfTheClipAfterIt()
    {
        var (timeline, _, _, b, _) = TestData.ThreeClipTimeline();

        timeline.SetLeadingGap(b, TimeSpan.FromSeconds(3));

        Assert.Equal(TimeSpan.FromSeconds(7), timeline.NextClipStart(TimeSpan.FromSeconds(5)));
    }

    [Fact]
    public void NextClipStart_OnAClipBoundary_IsThatBoundary()
    {
        var (timeline, _, _, _, _) = TestData.ThreeClipTimeline();

        Assert.Equal(TimeSpan.FromSeconds(4), timeline.NextClipStart(TimeSpan.FromSeconds(4)));
    }

    [Fact]
    public void NextClipStart_PastTheLastClip_IsTheTotalDuration()
    {
        var (timeline, _, _, _, _) = TestData.ThreeClipTimeline();

        Assert.Equal(timeline.TotalDuration, timeline.NextClipStart(TimeSpan.FromSeconds(12)));
    }

    [Fact]
    public void SetLeadingGap_RejectsANegativeGap()
    {
        var (timeline, _, _, b, _) = TestData.ThreeClipTimeline();

        timeline.SetLeadingGap(b, TimeSpan.FromSeconds(-2));

        Assert.Equal(TimeSpan.Zero, b.LeadingGap);
    }

    [Fact]
    public void Clip_RejectsAnOutThatIsNotAfterIn()
    {
        var sourceId = Guid.NewGuid();

        Assert.Throws<ArgumentException>(() =>
            Clip.Create(sourceId, TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(5)));
    }
}
