using System.Text.Json;
using VideoTinyTool.Application;
using VideoTinyTool.Localization;

namespace VideoTinyTool.Tests.Localization;

public class LocalizationKeyParityTests
{
    private static readonly string[] Languages = ["en", "ru"];
    private static readonly string[] PluralForms = ["one", "few", "many", "other"];

    [Fact]
    public void ShippedLanguagesCarryTheSameKeys()
    {
        var english = Groups("en");
        var russian = Groups("ru");

        Assert.Empty(english.Except(russian).Order());
        Assert.Empty(russian.Except(english).Order());
    }

    [Fact]
    public void EveryPluralGroupCarriesTheFormsItsLanguageNeeds()
    {
        foreach (var language in Languages)
        {
            var keys = Keys(language);

            foreach (var group in keys.Where(IsPluralForm).Select(Group).Distinct())
            {
                foreach (var form in RequiredForms(language))
                {
                    Assert.Contains($"{group}.{form}", keys);
                }
            }
        }
    }

    [Fact]
    public void EveryValueIsANonEmptyString()
    {
        foreach (var language in Languages)
        {
            foreach (var (key, value) in Flatten(language))
            {
                Assert.False(string.IsNullOrWhiteSpace(value), $"{language}: {key}");
            }
        }
    }

    private static IEnumerable<string> RequiredForms(string language)
    {
        var rule = Flatten(language).GetValueOrDefault(LocalizationCatalog.PluralKey, PluralRules.RuleFor(language));
        return Enumerable.Range(0, 201).Select(count => PluralRules.Form(rule, count)).Distinct();
    }

    private static bool IsPluralForm(string key) => PluralForms.Contains(key[(key.LastIndexOf('.') + 1)..]);

    private static string Group(string key) => key[..key.LastIndexOf('.')];

    private static HashSet<string> Keys(string language) => [.. Flatten(language).Keys];

    private static HashSet<string> Groups(string language) =>
        [.. Keys(language).Select(key => IsPluralForm(key) ? Group(key) : key)];

    private static Dictionary<string, string> Flatten(string language)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(AppPaths.LocalizationFile(language)));

        var target = new Dictionary<string, string>();
        Collect(document.RootElement, string.Empty, target);
        return target;
    }

    private static void Collect(JsonElement element, string prefix, Dictionary<string, string> target)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            target[prefix] = element.GetString() ?? string.Empty;
            return;
        }

        foreach (var property in element.EnumerateObject())
        {
            Collect(property.Value, prefix.Length == 0 ? property.Name : $"{prefix}.{property.Name}", target);
        }
    }
}
