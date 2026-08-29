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
    public void Clip_RejectsAnOutThatIsNotAfterIn()
    {
        var sourceId = Guid.NewGuid();

        Assert.Throws<ArgumentException>(() =>
            Clip.Create(sourceId, TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(5)));
    }
}
