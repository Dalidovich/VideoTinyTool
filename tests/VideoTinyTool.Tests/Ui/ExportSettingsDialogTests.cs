using VideoTinyTool.Application;
using VideoTinyTool.Ui.Widgets;

namespace VideoTinyTool.Tests.Ui;

public class ExportSettingsDialogTests
{
    [Fact]
    public void ApplyCarriesUntouchedValuesOverEvenWhenTheyAreNotPresets()
    {
        var current = new ExportSettings
        {
            Container = "mkv",
            VideoCodec = "libx265",
            Crf = 21,
            Preset = "slow",
            Width = 1440,
            Height = 1080,
            FrameRate = 48,
            Speed = 1.3,
            AudioCodec = "ac3",
            AudioBitrateKbps = 224
        };

        var target = new ExportSettings();
        new ExportSettingsDialog(current).Apply(target);

        Assert.Equal("mkv", target.Container);
        Assert.Equal("libx265", target.VideoCodec);
        Assert.Equal(21, target.Crf);
        Assert.Equal("slow", target.Preset);
        Assert.Equal(1440, target.Width);
        Assert.Equal(1080, target.Height);
        Assert.Equal(48, target.FrameRate);
        Assert.Equal(1.3, target.Speed);
        Assert.Equal("ac3", target.AudioCodec);
        Assert.Equal(224, target.AudioBitrateKbps);
    }

    [Fact]
    public void AudioOnlyDialogAppliesOnlyTheSoundRows()
    {
        var current = new ExportSettings
        {
            Width = 1280,
            Height = 720,
            Speed = 1.5,
            AudioContainer = "m4a",
            AudioBitrateKbps = 256
        };

        var target = new ExportSettings();
        new ExportSettingsDialog(current, audioOnly: true).Apply(target);

        Assert.Equal("m4a", target.AudioContainer);
        Assert.Equal(256, target.AudioBitrateKbps);
        Assert.Equal(1.5, target.Speed);
        Assert.Equal(1920, target.Width);
        Assert.Equal(1080, target.Height);
    }
}
