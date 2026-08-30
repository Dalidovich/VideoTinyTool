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
        Assert.Equal("ac3", target.AudioCodec);
        Assert.Equal(224, target.AudioBitrateKbps);
    }
}
