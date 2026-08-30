using VideoTinyTool.Domain;
using VideoTinyTool.Ui;

namespace VideoTinyTool.Tests.Ui;

public class OverlayGeometryTests
{
    private const float VideoWidth = 800f;
    private const float VideoHeight = 450f;

    private static readonly OverlayTransform Quarter = new(0.25f, 0.2f, 0.25f, 1f, 1f);

    [Fact]
    public void ProjectScalesTheTransformIntoVideoPixels()
    {
        var box = OverlayGeometry.Project(Quarter, VideoWidth, VideoHeight, 2.0);

        Assert.Equal(200f, box.Left);
        Assert.Equal(90f, box.Top);
        Assert.Equal(200f, box.Width);
        Assert.Equal(100f, box.Height);
    }

    [Fact]
    public void ProjectFallsBackToWidescreenForANonPositiveAspect()
    {
        var box = OverlayGeometry.Project(Quarter, VideoWidth, VideoHeight, 0);

        Assert.Equal(200f * 9f / 16f, box.Height, 3);
    }

    [Theory]
    [InlineData(200f, 90f, OverlayHandle.TopLeft)]
    [InlineData(400f, 90f, OverlayHandle.TopRight)]
    [InlineData(200f, 190f, OverlayHandle.BottomLeft)]
    [InlineData(400f, 190f, OverlayHandle.BottomRight)]
    [InlineData(300f, 140f, OverlayHandle.Body)]
    [InlineData(600f, 400f, OverlayHandle.None)]
    public void HitTestRecognisesCornersBodyAndMisses(float x, float y, OverlayHandle expected)
    {
        var box = OverlayGeometry.Project(Quarter, VideoWidth, VideoHeight, 2.0);

        Assert.Equal(expected, OverlayGeometry.HitTest(box, x, y));
    }

    [Fact]
    public void HitTestGivesCornersPriorityOverTheBody()
    {
        var box = OverlayGeometry.Project(Quarter, VideoWidth, VideoHeight, 2.0);

        Assert.Equal(OverlayHandle.TopLeft, OverlayGeometry.HitTest(box, 204f, 94f));
    }

    [Fact]
    public void MoveShiftsTheTopLeftByTheDragDelta()
    {
        var moved = OverlayGeometry.Move(Quarter, VideoWidth, VideoHeight, 80f, -45f);

        Assert.Equal(0.35f, moved.X, 4);
        Assert.Equal(0.1f, moved.Y, 4);
        Assert.Equal(Quarter.Width, moved.Width);
    }

    [Fact]
    public void MoveKeepsTheTopLeftInsideTheFrame()
    {
        var moved = OverlayGeometry.Move(Quarter, VideoWidth, VideoHeight, 10_000f, -10_000f);

        Assert.Equal(1f, moved.X);
        Assert.Equal(0f, moved.Y);
    }

    [Fact]
    public void MoveIgnoresADegenerateVideoRectangle()
    {
        Assert.Equal(Quarter, OverlayGeometry.Move(Quarter, 0f, 0f, 25f, 25f));
    }

    [Fact]
    public void ResizeFromTheBottomRightKeepsTheTopLeftAnchored()
    {
        var resized = OverlayGeometry.Resize(Quarter, OverlayHandle.BottomRight, VideoWidth, VideoHeight, 2.0, 80f, 40f);

        Assert.Equal(Quarter.X, resized.X, 4);
        Assert.Equal(Quarter.Y, resized.Y, 4);
        Assert.Equal(0.35f, resized.Width, 4);
    }

    [Fact]
    public void ResizeFromTheTopLeftKeepsTheBottomRightAnchored()
    {
        var resized = OverlayGeometry.Resize(Quarter, OverlayHandle.TopLeft, VideoWidth, VideoHeight, 2.0, 80f, 40f);

        var before = OverlayGeometry.Project(Quarter, VideoWidth, VideoHeight, 2.0);
        var after = OverlayGeometry.Project(resized, VideoWidth, VideoHeight, 2.0);

        Assert.Equal(before.Right, after.Right, 3);
        Assert.Equal(before.Bottom, after.Bottom, 3);
        Assert.Equal(0.15f, resized.Width, 4);
    }

    [Fact]
    public void ResizePreservesTheSourceAspect()
    {
        var resized = OverlayGeometry.Resize(Quarter, OverlayHandle.BottomRight, VideoWidth, VideoHeight, 2.0, 120f, 0f);
        var box = OverlayGeometry.Project(resized, VideoWidth, VideoHeight, 2.0);

        Assert.Equal(2.0, (double)box.Width / box.Height, 3);
    }

    [Fact]
    public void ResizeStopsAtTheMinimumWidth()
    {
        var resized = OverlayGeometry.Resize(Quarter, OverlayHandle.BottomRight, VideoWidth, VideoHeight, 2.0, -10_000f, -10_000f);

        Assert.Equal(OverlayTransform.MinWidth, resized.Width);
    }

    [Fact]
    public void ResizeStopsAtTheFullFrameWidth()
    {
        var resized = OverlayGeometry.Resize(Quarter, OverlayHandle.BottomRight, VideoWidth, VideoHeight, 2.0, 10_000f, 10_000f);

        Assert.Equal(1f, resized.Width);
    }

    [Fact]
    public void ResizeLeavesOpacityAndVolumeAlone()
    {
        var start = new OverlayTransform(0.25f, 0.2f, 0.25f, 0.4f, 0.7f);
        var resized = OverlayGeometry.Resize(start, OverlayHandle.TopRight, VideoWidth, VideoHeight, 2.0, 40f, -20f);

        Assert.Equal(start.Opacity, resized.Opacity);
        Assert.Equal(start.Volume, resized.Volume);
    }

    [Fact]
    public void ResizeIgnoresNonCornerHandles()
    {
        Assert.Equal(Quarter, OverlayGeometry.Resize(Quarter, OverlayHandle.Body, VideoWidth, VideoHeight, 2.0, 40f, 40f));
        Assert.Equal(Quarter, OverlayGeometry.Resize(Quarter, OverlayHandle.None, VideoWidth, VideoHeight, 2.0, 40f, 40f));
    }
}
