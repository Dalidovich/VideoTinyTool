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
        Assert.Equal(expected, PluralRules.Form(PluralRules.OneOther, count));
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
    public void SlavicHasThreeForms(int count, string expected)
    {
        Assert.Equal(expected, PluralRules.Form(PluralRules.Slavic, count));
    }

    [Fact]
    public void UnknownRuleFallsBackToOneOther()
    {
        Assert.Equal("other", PluralRules.Form("zz", 3));
    }

    [Theory]
    [InlineData("ru", PluralRules.Slavic)]
    [InlineData("UK", PluralRules.Slavic)]
    [InlineData("en", PluralRules.OneOther)]
    [InlineData("de", PluralRules.OneOther)]
    public void LanguagesWithoutADeclaredRuleGetOneByCode(string language, string expected)
    {
        Assert.Equal(expected, PluralRules.RuleFor(language));
    }
}
