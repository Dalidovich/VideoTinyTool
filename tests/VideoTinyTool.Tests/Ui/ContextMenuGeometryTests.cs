using VideoTinyTool.Ui;

namespace VideoTinyTool.Tests.Ui;

public class ContextMenuGeometryTests
{
    [Fact]
    public void MenuOpensAtTheAnchorWhenItFits()
    {
        Assert.Equal(120f, ContextMenuGeometry.Place(120f, 150f, 800f));
    }

    [Fact]
    public void MenuFlipsBackWhenItWouldOverflow()
    {
        Assert.Equal(800f - 150f - ContextMenuGeometry.ScreenMargin, ContextMenuGeometry.Place(800f, 150f, 800f));
    }

    [Fact]
    public void FlippedMenuStaysInsideTheWindow()
    {
        Assert.Equal(ContextMenuGeometry.ScreenMargin, ContextMenuGeometry.Place(40f, 150f, 160f));
    }

    [Fact]
    public void MenuTallerThanTheWindowStartsAtTheMargin()
    {
        Assert.Equal(ContextMenuGeometry.ScreenMargin, ContextMenuGeometry.Place(300f, 400f, 300f));
    }
}
