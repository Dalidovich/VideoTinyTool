using VideoTinyTool.Ui;

namespace VideoTinyTool.Tests.Ui;

public class LaneGeometryTests
{
    [Fact]
    public void SingleTrackKeepsTheWholeLaneArea()
    {
        Assert.Equal(180f, LaneGeometry.LaneHeight(180f, 1));
        Assert.Equal(0f, LaneGeometry.MaxScroll(180f, 1));
    }

    [Theory]
    [InlineData(180f, 2, 90f)]
    [InlineData(180f, 3, 60f)]
    [InlineData(180f, 4, 45f)]
    public void LanesShareTheAreaWhileTheyFit(float available, int trackCount, float expected)
    {
        Assert.Equal(expected, LaneGeometry.LaneHeight(available, trackCount));
        Assert.Equal(0f, LaneGeometry.MaxScroll(available, trackCount));
    }

    [Fact]
    public void LaneHeightStopsAtTheFloorAndTheStackOverflows()
    {
        Assert.Equal(LaneGeometry.MinLaneHeight, LaneGeometry.LaneHeight(120f, 4));
        Assert.Equal(176f, LaneGeometry.StackHeight(120f, 4));
        Assert.Equal(56f, LaneGeometry.MaxScroll(120f, 4));
    }

    [Fact]
    public void BaseTrackSitsAtTheBottomOfTheStack()
    {
        Assert.Equal(90f, LaneGeometry.LaneOffset(180f, 2, 2, 0, 0f));
        Assert.Equal(0f, LaneGeometry.LaneOffset(180f, 2, 2, 1, 0f));
    }

    [Fact]
    public void ScrollShiftsTheWholeStackUp()
    {
        Assert.Equal(132f - 40f, LaneGeometry.LaneOffset(120f, 4, 4, 0, 40f));
        Assert.Equal(-40f, LaneGeometry.LaneOffset(120f, 4, 4, 3, 40f));
    }

    [Fact]
    public void ScrollIsClampedToTheOverflow()
    {
        Assert.Equal(56f, LaneGeometry.ClampScroll(400f, 120f, 4));
        Assert.Equal(0f, LaneGeometry.ClampScroll(-20f, 120f, 4));
        Assert.Equal(0f, LaneGeometry.ClampScroll(30f, 180f, 2));
    }

    [Theory]
    [InlineData(0f, 1)]
    [InlineData(89f, 1)]
    [InlineData(90f, 0)]
    [InlineData(179f, 0)]
    public void TrackIndexFollowsTheVisualOrder(float offsetY, int expected)
    {
        Assert.Equal(expected, LaneGeometry.TrackIndexAt(180f, 2, 2, 0f, offsetY));
    }

    [Theory]
    [InlineData(-1f)]
    [InlineData(180f)]
    public void PointsOutsideTheLaneAreaResolveToNoTrack(float offsetY)
    {
        Assert.Equal(-1, LaneGeometry.TrackIndexAt(180f, 2, 2, 0f, offsetY));
    }

    [Fact]
    public void ScrolledStackResolvesTheTrackUnderTheCursor()
    {
        Assert.Equal(2, LaneGeometry.TrackIndexAt(120f, 4, 4, 44f, 0f));
        Assert.Equal(0, LaneGeometry.TrackIndexAt(120f, 4, 4, 56f, 119f));
    }

    [Theory]
    [InlineData(0, 3)]
    [InlineData(1, 2)]
    [InlineData(2, 1)]
    [InlineData(3, 0)]
    public void VideoOnlyStackKeepsTheBaseTrackAtTheBottom(int trackIndex, int expectedRow)
    {
        Assert.Equal(expectedRow, LaneGeometry.RowOf(4, 4, trackIndex));
        Assert.Equal(trackIndex, LaneGeometry.TrackIndexAtRow(4, 4, expectedRow));
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(1, 1)]
    [InlineData(2, 2)]
    public void AudioOnlyStackKeepsTheTrackOrder(int trackIndex, int expectedRow)
    {
        Assert.Equal(expectedRow, LaneGeometry.RowOf(0, 3, trackIndex));
        Assert.Equal(trackIndex, LaneGeometry.TrackIndexAtRow(0, 3, expectedRow));
    }

    [Theory]
    [InlineData(1, 0)]
    [InlineData(0, 1)]
    [InlineData(2, 2)]
    [InlineData(3, 3)]
    [InlineData(4, 4)]
    public void MixedStackPutsVideoAboveTheBaseTrackAndAudioBelowIt(int trackIndex, int expectedRow)
    {
        Assert.Equal(expectedRow, LaneGeometry.RowOf(2, 5, trackIndex));
        Assert.Equal(trackIndex, LaneGeometry.TrackIndexAtRow(2, 5, expectedRow));
    }

    [Fact]
    public void RowsOutsideTheStackResolveToNoTrack()
    {
        Assert.Equal(-1, LaneGeometry.RowOf(2, 5, -1));
        Assert.Equal(-1, LaneGeometry.RowOf(2, 5, 5));
        Assert.Equal(-1, LaneGeometry.TrackIndexAtRow(2, 5, -1));
        Assert.Equal(-1, LaneGeometry.TrackIndexAtRow(2, 5, 5));
    }

    [Fact]
    public void AudioLanesSitUnderTheBaseTrack()
    {
        Assert.Equal(0f, LaneGeometry.LaneOffset(180f, 2, 3, 1, 0f));
        Assert.Equal(60f, LaneGeometry.LaneOffset(180f, 2, 3, 0, 0f));
        Assert.Equal(120f, LaneGeometry.LaneOffset(180f, 2, 3, 2, 0f));
    }

    [Fact]
    public void PointerResolvesAudioLanesUnderTheBaseTrack()
    {
        Assert.Equal(1, LaneGeometry.TrackIndexAt(180f, 2, 3, 0f, 0f));
        Assert.Equal(0, LaneGeometry.TrackIndexAt(180f, 2, 3, 0f, 60f));
        Assert.Equal(2, LaneGeometry.TrackIndexAt(180f, 2, 3, 0f, 120f));
    }

    [Fact]
    public void DropStartKeepsTheGrabOffset()
    {
        Assert.Equal(
            TimeSpan.FromSeconds(7),
            LaneGeometry.DropStart(TimeSpan.FromSeconds(9), TimeSpan.FromSeconds(2)));
    }

    [Fact]
    public void DropStartClampsToTheTimelineStart()
    {
        Assert.Equal(
            TimeSpan.Zero,
            LaneGeometry.DropStart(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(4)));
    }
}
