using VideoTinyTool.Application;
using VideoTinyTool.Localization;

namespace VideoTinyTool.Tests.Localization;

public class LocalizationCatalogTests
{
    [Fact]
    public void BuiltInCatalogReadsTheEmbeddedEnglishFile()
    {
        var catalog = LocalizationCatalog.BuiltIn();

        Assert.Equal(LocalizationCatalog.DefaultLanguage, catalog.Language);
        Assert.Null(catalog.Warning);
        Assert.Equal("Import…", catalog.Text("toolbar.import"));
    }

    [Fact]
    public void UnknownKeyFallsBackToTheKeyItself()
    {
        Assert.Equal("toolbar.missing", LocalizationCatalog.BuiltIn().Text("toolbar.missing"));
    }

    [Fact]
    public void FormatFillsPlaceholders()
    {
        Assert.Equal("1920×1080 · 30 fps", LocalizationCatalog.BuiltIn().Format("preview.format", 1920, 1080, 30));
    }

    [Theory]
    [InlineData(1, "1 file")]
    [InlineData(2, "2 files")]
    [InlineData(21, "21 files")]
    public void EnglishPluralsUseOneAndOther(int count, string expected)
    {
        Assert.Equal(expected, LocalizationCatalog.BuiltIn().Plural("sources.fileCount", count, count));
    }

    [Fact]
    public void ShippedLanguageFileReplacesTheBuiltInText()
    {
        var catalog = LocalizationCatalog.Load("ru");

        Assert.Null(catalog.Warning);
        Assert.Equal("Импорт…", catalog.Text("toolbar.import"));
    }

    [Theory]
    [InlineData(1, "1 файл")]
    [InlineData(3, "3 файла")]
    [InlineData(11, "11 файлов")]
    [InlineData(21, "21 файл")]
    public void RussianPluralsUseOneFewAndMany(int count, string expected)
    {
        Assert.Equal(expected, LocalizationCatalog.Load("ru").Plural("sources.fileCount", count, count));
    }

    [Fact]
    public void KeysMissingFromALanguageFileFallBackToEnglish()
    {
        Assert.Equal("{0} files", LocalizationCatalog.Load("ru").Text("sources.fileCount.other"));
    }

    [Fact]
    public void MissingLanguageFileWarnsAndKeepsEnglish()
    {
        var catalog = LocalizationCatalog.Load("zz");

        Assert.Contains(AppPaths.LocalizationFile("zz"), catalog.Warning);
        Assert.Equal("Import…", catalog.Text("toolbar.import"));
    }
}
