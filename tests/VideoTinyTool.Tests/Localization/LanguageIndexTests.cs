using VideoTinyTool.Application;
using VideoTinyTool.Localization;

namespace VideoTinyTool.Tests.Localization;

public class LanguageIndexTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "vtt-languages-" + Guid.NewGuid().ToString("N"));

    public LanguageIndexTests() => Directory.CreateDirectory(_directory);

    public void Dispose() => Directory.Delete(_directory, true);

    [Fact]
    public void ShippedFolderOffersEveryLanguageBesideTheExecutable()
    {
        var codes = LanguageIndex.Scan(AppPaths.LocalizationDirectory).Select(option => option.Code);

        Assert.Contains("en", codes);
        Assert.Contains("ru", codes);
    }

    [Fact]
    public void ADroppedInFileBecomesAnOptionWithoutACodeChange()
    {
        Write("de", """{ "language": { "name": "Deutsch" } }""");

        var option = Assert.Single(LanguageIndex.Scan(_directory), candidate => candidate.Code == "de");

        Assert.Equal("Deutsch", option.Name);
        Assert.Equal("DE", option.Badge);
    }

    [Fact]
    public void AFileWithoutANameFallsBackToItsCode()
    {
        Write("fr", """{ "toolbar": { "import": "Importer…" } }""");

        Assert.Equal("FR", Assert.Single(LanguageIndex.Scan(_directory), candidate => candidate.Code == "fr").Name);
    }

    [Fact]
    public void AnUnreadableFileStillOffersItsCode()
    {
        Write("it", "{ broken");

        Assert.Equal("IT", Assert.Single(LanguageIndex.Scan(_directory), candidate => candidate.Code == "it").Name);
    }

    [Fact]
    public void EnglishIsOfferedEvenWhenTheFolderIsEmpty()
    {
        Assert.Equal(["en"], LanguageIndex.Scan(_directory).Select(option => option.Code));
    }

    [Fact]
    public void OptionsAreOrderedByCode()
    {
        Write("sv", "{}");
        Write("de", "{}");
        Write("en", "{}");

        Assert.Equal(["de", "en", "sv"], LanguageIndex.Scan(_directory).Select(option => option.Code));
    }

    [Fact]
    public void FilesThatCannotBeALanguageCodeAreIgnored()
    {
        Write("not a code", "{}");

        Assert.Equal(["en"], LanguageIndex.Scan(_directory).Select(option => option.Code));
    }

    [Theory]
    [InlineData(" RU ", "ru")]
    [InlineData("pt-br", "pt-br")]
    [InlineData("", null)]
    [InlineData("ru/../etc", null)]
    [InlineData("aaaaaaaaaaaaaaaaa", null)]
    public void CodesAreNormalizedAndValidated(string value, string? expected)
    {
        Assert.Equal(expected, LanguageIndex.Normalize(value));
    }

    private void Write(string code, string json) =>
        File.WriteAllText(Path.Combine(_directory, code + ".json"), json);
}
