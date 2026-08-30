using VideoTinyTool.Localization;

namespace VideoTinyTool.Tests.Localization;

public class PluralRulesTests
{
    [Theory]
    [InlineData(0, "other")]
    [InlineData(1, "one")]
    [InlineData(2, "other")]
    public void EnglishHasTwoForms(int count, string expected)
    {
        Assert.Equal(expected, PluralRules.Form("en", count));
    }

    [Theory]
    [InlineData(1, "one")]
    [InlineData(2, "few")]
    [InlineData(5, "many")]
    [InlineData(11, "many")]
    [InlineData(12, "many")]
    [InlineData(21, "one")]
    [InlineData(22, "few")]
    [InlineData(111, "many")]
    public void RussianHasThreeForms(int count, string expected)
    {
        Assert.Equal(expected, PluralRules.Form("ru", count));
    }

    [Fact]
    public void UnknownLanguageUsesTheEnglishRule()
    {
        Assert.Equal("other", PluralRules.Form("zz", 3));
    }
}
