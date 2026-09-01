using VideoTinyTool.Ui;

namespace VideoTinyTool.Tests.Ui;

public class SnapGeometryTests
{
    private static TimeSpan Seconds(double value) => TimeSpan.FromSeconds(value);

    [Fact]
    public void PointerSticksToTheNearestTargetInsideTheRadius()
    {
        var targets = new[] { Seconds(2), Seconds(5) };

        Assert.True(SnapGeometry.TrySnap(Seconds(2.1), targets, 40f, out var snapped));
        Assert.Equal(Seconds(2), snapped);
    }

    [Fact]
    public void PointerKeepsItsPlaceOutsideTheRadius()
    {
        var targets = new[] { Seconds(2) };
        var pointer = Seconds(2.5);

        Assert.False(SnapGeometry.TrySnap(pointer, targets, 40f, out var snapped));
        Assert.Equal(pointer, snapped);
        Assert.Equal(pointer, SnapGeometry.Snap(pointer, targets, 40f));
    }

    [Fact]
    public void ZoomDecidesHowFarTheMagnetReaches()
    {
        var targets = new[] { Seconds(10) };
        var pointer = Seconds(10.5);

        Assert.False(SnapGeometry.TrySnap(pointer, targets, 40f, out _));
        Assert.True(SnapGeometry.TrySnap(pointer, targets, 4f, out var wide));
        Assert.Equal(Seconds(10), wide);
    }

    [Fact]
    public void ClosestTargetWinsWhenTwoAreInRange()
    {
        var targets = new[] { Seconds(4.9), Seconds(5.05) };

        Assert.Equal(Seconds(5.05), SnapGeometry.Snap(Seconds(5), targets, 40f));
    }

    [Fact]
    public void EmptyTargetsAndDeadZoomLeaveTheValueAlone()
    {
        Assert.False(SnapGeometry.TrySnap(Seconds(1), [], 40f, out _));
        Assert.False(SnapGeometry.TrySnap(Seconds(1), [Seconds(1)], 0f, out _));
        Assert.Equal(Seconds(3), SnapGeometry.SnapSpan(Seconds(3), Seconds(2), [], 40f));
    }

    [Fact]
    public void ClipHeadSticksToATarget()
    {
        var start = SnapGeometry.SnapSpan(Seconds(4.05), Seconds(2), [Seconds(4)], 40f);

        Assert.Equal(Seconds(4), start);
    }

    [Fact]
    public void ClipTailSticksToATarget()
    {
        var start = SnapGeometry.SnapSpan(Seconds(3.95), Seconds(2), [Seconds(6)], 40f);

        Assert.Equal(Seconds(4), start);
    }

    [Fact]
    public void TheNearerEdgeWinsWhenBothCouldStick()
    {
        var start = SnapGeometry.SnapSpan(Seconds(4.1), Seconds(2), [Seconds(4), Seconds(6.05)], 40f);

        Assert.Equal(Seconds(4.05), start);
    }

    [Fact]
    public void TailNeverDragsTheClipBeforeZero()
    {
        var start = SnapGeometry.SnapSpan(Seconds(0.05), Seconds(2), [Seconds(1)], 40f);

        Assert.Equal(Seconds(0.05), start);
    }
}
