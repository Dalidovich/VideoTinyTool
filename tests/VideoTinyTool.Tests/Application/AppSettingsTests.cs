using VideoTinyTool.Application;

namespace VideoTinyTool.Tests.Application;

public class AppSettingsTests
{
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
