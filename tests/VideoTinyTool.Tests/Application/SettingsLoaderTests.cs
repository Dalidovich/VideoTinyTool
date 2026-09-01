using VideoTinyTool.Application;

namespace VideoTinyTool.Tests.Application;

public class SettingsLoaderTests : IDisposable
{
    private readonly string _directory = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "vtt-settings-" + Guid.NewGuid().ToString("N"));

    public SettingsLoaderTests() => Directory.CreateDirectory(_directory);

    public void Dispose() => Directory.Delete(_directory, true);

    [Fact]
    public void SavingTheLanguageCreatesTheFileWhenItIsMissing()
    {
        Assert.Null(SettingsLoader.TrySaveLanguage(SettingsFile, "ru"));
        Assert.Equal("ru", SettingsLoader.LoadOrCreate(SettingsFile).Ui.Language);
    }

    [Fact]
    public void SavingTheLanguageKeepsEveryOtherSetting()
    {
        File.WriteAllText(SettingsFile, """
        {
          "export": { "container": "mkv", "crf": 31 },
          "window": { "width": 1234, "height": 777 },
          "ui": { "language": "en" }
        }
        """);

        Assert.Null(SettingsLoader.TrySaveLanguage(SettingsFile, "ru"));

        var settings = SettingsLoader.LoadOrCreate(SettingsFile);

        Assert.Equal("ru", settings.Ui.Language);
        Assert.Equal("mkv", settings.Export.Container);
        Assert.Equal(31, settings.Export.Crf);
        Assert.Equal(1234, settings.Window.Width);
    }

    [Fact]
    public void SavingTheLanguageDoesNotDuplicateADifferentlyCasedKey()
    {
        File.WriteAllText(SettingsFile, """{ "Ui": { "Language": "en" } }""");

        Assert.Null(SettingsLoader.TrySaveLanguage(SettingsFile, "ru"));

        Assert.DoesNotContain("language", File.ReadAllText(SettingsFile));
        Assert.Equal("ru", SettingsLoader.LoadOrCreate(SettingsFile).Ui.Language);
    }

    [Fact]
    public void SavingTheLanguageKeepsUnknownSections()
    {
        File.WriteAllText(SettingsFile, """{ "future": { "kept": true } }""");

        Assert.Null(SettingsLoader.TrySaveLanguage(SettingsFile, "ru"));

        Assert.Contains("\"kept\"", File.ReadAllText(SettingsFile));
    }

    [Fact]
    public void AnUnwritablePathIsReportedInsteadOfThrowing()
    {
        Assert.NotNull(SettingsLoader.TrySaveLanguage(Path.Combine(_directory, "missing", "settings.json"), "ru"));
    }

    private string SettingsFile => Path.Combine(_directory, "settings.json");
}
