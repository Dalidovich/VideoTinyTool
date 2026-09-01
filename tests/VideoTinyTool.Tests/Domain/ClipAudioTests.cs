using VideoTinyTool.Domain;

namespace VideoTinyTool.Tests.Domain;

public class ClipAudioTests
{
    [Fact]
    public void Default_IsFullVolumeAndUnmuted()
    {
        Assert.Equal(1f, ClipAudio.Default.Volume);
        Assert.False(ClipAudio.Default.Muted);
        Assert.Equal(1f, ClipAudio.Default.Gain);
    }

    [Theory]
    [InlineData(-2f, 0f)]
    [InlineData(0.25f, 0.25f)]
    [InlineData(4f, 1f)]
    public void Clamped_PullsVolumeIntoRangeAndKeepsMute(float volume, float expected)
    {
        var clamped = new ClipAudio(volume, true).Clamped();

        Assert.Equal(expected, clamped.Volume);
        Assert.True(clamped.Muted);
    }

    [Theory]
    [InlineData(0.75f, false, 0.75f)]
    [InlineData(0.75f, true, 0f)]
    [InlineData(0f, false, 0f)]
    [InlineData(3f, false, 1f)]
    public void Gain_FoldsMuteAndClamping(float volume, bool muted, float expected)
    {
        Assert.Equal(expected, new ClipAudio(volume, muted).Gain);
    }

    [Fact]
    public void SetClipAudio_StoresTheClampedValue()
    {
        var timeline = new Timeline();
        var clip = TestData.Clip(Guid.NewGuid(), 0, 2);
        timeline.Add(clip);

        timeline.SetClipAudio(clip, new ClipAudio(1.5f, false));

        Assert.Equal(new ClipAudio(1f, false), clip.Audio);
    }
}
