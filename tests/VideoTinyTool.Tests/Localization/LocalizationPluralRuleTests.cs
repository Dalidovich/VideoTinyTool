using VideoTinyTool.Application;
using VideoTinyTool.Localization;

namespace VideoTinyTool.Tests.Localization;

public class LocalizationPluralRuleTests : IDisposable
{
    private readonly List<string> _written = new();

    public void Dispose()
    {
        foreach (var path in _written)
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void ShippedFilesDeclareTheirRule()
    {
        Assert.Equal(PluralRules.OneOther, LocalizationCatalog.BuiltIn().PluralRule);
        Assert.Equal(PluralRules.Slavic, LocalizationCatalog.Load("ru").PluralRule);
    }

    [Fact]
    public void ADeclaredRuleDrivesTheChosenForm()
    {
        Write("qs", """
        {
          "language": { "name": "Qsish", "plural": "slavic" },
          "sources": { "fileCount": { "one": "{0} qs one", "few": "{0} qs few", "many": "{0} qs many" } }
        }
        """);

        var catalog = LocalizationCatalog.Load("qs");

        Assert.Equal(PluralRules.Slavic, catalog.PluralRule);
        Assert.Equal("3 qs few", catalog.Plural("sources.fileCount", 3, 3));
        Assert.Equal("11 qs many", catalog.Plural("sources.fileCount", 11, 11));
    }

    [Fact]
    public void AFileWithoutARuleUsesTheCodeInsteadOfTheEnglishBase()
    {
        Write("uk", """{ "language": { "name": "Qukish" } }""");

        Assert.Equal(PluralRules.Slavic, LocalizationCatalog.Load("uk").PluralRule);
    }

    [Fact]
    public void AnUnknownCodeWithoutARuleStaysOnOneOther()
    {
        Write("qn", """{ "language": { "name": "Qnish" } }""");

        Assert.Equal(PluralRules.OneOther, LocalizationCatalog.Load("qn").PluralRule);
    }

    [Fact]
    public void TheLanguageNameComesFromTheFile()
    {
        Write("qn", """{ "language": { "name": "Qnish" } }""");

        Assert.Equal("Qnish", LocalizationCatalog.Load("qn").Text(LocalizationCatalog.NameKey));
    }

    private void Write(string code, string json)
    {
        var path = AppPaths.LocalizationFile(code);
        File.WriteAllText(path, json);
        _written.Add(path);
    }
}
