using VideoTinyTool.Application;

namespace VideoTinyTool.Tests.Application;

public class AppSettingsTests
{
    [Theory]
    [InlineData(1.0, 1.0)]
    [InlineData(1.25, 1.25)]
    [InlineData(0.1, 0.5)]
    [InlineData(9.0, 3.0)]
    [InlineData(0.0, 1.0)]
    [InlineData(-2.0, 1.0)]
    [InlineData(double.NaN, 1.0)]
    public void SanitizeKeepsTheExportSpeedInsideTheSupportedRange(double speed, double expected)
    {
        var settings = new AppSettings { Export = { Speed = speed } };

        settings.Sanitize();

        Assert.Equal(expected, settings.Export.Speed);
    }

    [Theory]
    [InlineData("", "mp3")]
    [InlineData("   ", "mp3")]
    [InlineData(".m4a", "m4a")]
    [InlineData("m4a", "m4a")]
    public void SanitizeNormalisesTheAudioContainer(string container, string expected)
    {
        var settings = new AppSettings { Export = { AudioContainer = container } };

        settings.Sanitize();

        Assert.Equal(expected, settings.Export.AudioContainer);
    }

    [Theory]
    [InlineData("mp3", "libmp3lame")]
    [InlineData("m4a", "aac")]
    [InlineData("M4A", "aac")]
    public void AudioCodecFollowsTheAudioContainer(string container, string expected)
    {
        Assert.Equal(expected, ExportSettings.AudioCodecFor(container));
    }

    [Theory]
    [InlineData("ru", "ru")]
    [InlineData(" RU ", "ru")]
    [InlineData("pt-br", "pt-br")]
    [InlineData("", "en")]
    [InlineData("   ", "en")]
    [InlineData(@"..\..\windows\win", "en")]
    [InlineData("en/../../secrets", "en")]
    public void SanitizeKeepsOnlyUsableLanguageTokens(string language, string expected)
    {
        var settings = new AppSettings { Ui = { Language = language } };

        settings.Sanitize();

        Assert.Equal(expected, settings.Ui.Language);
    }
}
