using VideoTinyTool.Domain;

namespace VideoTinyTool.Tests.Domain;

public class OverlayTransformTests
{
    [Fact]
    public void Default_IsAlreadyInRange()
    {
        Assert.Equal(OverlayTransform.Default, OverlayTransform.Default.Clamped());
    }

    [Fact]
    public void Clamped_PullsValuesIntoRange()
    {
        var clamped = new OverlayTransform(-1f, 2f, 0f, 5f, -0.5f).Clamped();

        Assert.Equal(0f, clamped.X);
        Assert.Equal(1f, clamped.Y);
        Assert.Equal(OverlayTransform.MinWidth, clamped.Width);
        Assert.Equal(1f, clamped.Opacity);
        Assert.Equal(0f, clamped.Volume);
    }

    [Fact]
    public void Clamped_CapsWidthAtOne()
    {
        Assert.Equal(1f, new OverlayTransform(0f, 0f, 4f, 1f, 1f).Clamped().Width);
    }

    [Fact]
    public void NewClip_CarriesTheDefaultTransform()
    {
        var clip = TestData.Clip(Guid.NewGuid(), 0, 2);

        Assert.Equal(OverlayTransform.Default, clip.Overlay);
    }

    [Fact]
    public void SetOverlay_StoresTheClampedTransform()
    {
        var timeline = new Timeline();
        var clip = TestData.Clip(Guid.NewGuid(), 0, 2);
        timeline.Add(clip);

        timeline.SetOverlay(clip, new OverlayTransform(0.5f, 0.5f, 2f, 0.5f, 0.5f));

        Assert.Equal(new OverlayTransform(0.5f, 0.5f, 1f, 0.5f, 0.5f), clip.Overlay);
    }

    [Fact]
    public void WithBounds_CarriesGapAndTransform()
    {
        var timeline = new Timeline();
        var clip = TestData.Clip(Guid.NewGuid(), 0, 4);
        timeline.Add(clip);
        timeline.SetLeadingGap(clip, TimeSpan.FromSeconds(3));
        timeline.SetOverlay(clip, new OverlayTransform(0.1f, 0.2f, 0.3f, 0.4f, 0.5f));

        var copy = clip.WithBounds(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(2));

        Assert.Equal(TimeSpan.FromSeconds(3), copy.LeadingGap);
        Assert.Equal(clip.Overlay, copy.Overlay);
    }
}
