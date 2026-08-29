using VideoTinyTool.Domain;

namespace VideoTinyTool.Tests.Domain;

public class EditRulesTests
{
    private static readonly TimeSpan OneFrameAt25 = TimeSpan.FromSeconds(1.0 / 25);

    [Fact]
    public void MinimumDuration_IsOneSourceFrame()
    {
        var source = TestData.Source(frameRate: 25);

        Assert.Equal(OneFrameAt25, EditRules.MinimumDuration(source));
    }

    [Fact]
    public void MinimumDuration_FallsBackWhenTheSourceIsGone()
    {
        Assert.Equal(EditRules.FallbackMinimumDuration, EditRules.MinimumDuration(null));
    }

    [Fact]
    public void TrimIn_IsRefusedWhenItLeavesLessThanOneFrame()
    {
        var clip = TestData.Clip(Guid.NewGuid(), 1, 2);
        var desired = clip.Out - TimeSpan.FromMilliseconds(10);

        Assert.False(EditRules.TryTrimIn(clip, desired, OneFrameAt25, out _));
    }

    [Fact]
    public void TrimIn_KeepsExactlyOneFrame()
    {
        var clip = TestData.Clip(Guid.NewGuid(), 1, 2);
        var desired = clip.Out - OneFrameAt25;

        Assert.True(EditRules.TryTrimIn(clip, desired, OneFrameAt25, out var result));
        Assert.Equal(desired, result);
    }

    [Fact]
    public void TrimIn_ClampsToTheStartOfTheSource()
    {
        var clip = TestData.Clip(Guid.NewGuid(), 1, 2);

        Assert.True(EditRules.TryTrimIn(clip, TimeSpan.FromSeconds(-4), OneFrameAt25, out var result));
        Assert.Equal(TimeSpan.Zero, result);
    }

    [Fact]
    public void TrimOut_IsRefusedWhenItLeavesLessThanOneFrame()
    {
        var clip = TestData.Clip(Guid.NewGuid(), 1, 2);
        var desired = clip.In + TimeSpan.FromMilliseconds(10);

        Assert.False(EditRules.TryTrimOut(clip, desired, OneFrameAt25, TimeSpan.FromSeconds(60), out _));
    }

    [Fact]
    public void TrimOut_ClampsToTheEndOfTheSource()
    {
        var clip = TestData.Clip(Guid.NewGuid(), 1, 2);

        Assert.True(EditRules.TryTrimOut(
            clip,
            TimeSpan.FromSeconds(90),
            OneFrameAt25,
            TimeSpan.FromSeconds(60),
            out var result));

        Assert.Equal(TimeSpan.FromSeconds(60), result);
    }

    [Fact]
    public void Split_IsRefusedExactlyOnAClipBoundary()
    {
        var clip = TestData.Clip(Guid.NewGuid(), 0, 4);

        Assert.False(EditRules.CanSplit(clip, TimeSpan.Zero, OneFrameAt25));
        Assert.False(EditRules.CanSplit(clip, clip.Duration, OneFrameAt25));
    }

    [Fact]
    public void Split_IsAllowedInsideTheClip()
    {
        var clip = TestData.Clip(Guid.NewGuid(), 0, 4);

        Assert.True(EditRules.CanSplit(clip, TimeSpan.FromSeconds(2), OneFrameAt25));
    }
}
