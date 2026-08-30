using System.Globalization;
using System.Text.Json;
using VideoTinyTool.Application;

namespace VideoTinyTool.Localization;

public sealed class LocalizationCatalog
{
    public const string DefaultLanguage = "en";

    private const string BuiltInResource = "localization/en.json";
    private const string OtherForm = "other";

    private static readonly JsonDocumentOptions ReadOptions = new()
    {
        CommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    private readonly Dictionary<string, string> _strings;

    private LocalizationCatalog(string language, Dictionary<string, string> strings)
    {
        Language = language;
        _strings = strings;
    }

    public string Language { get; }

    public string? Warning { get; private set; }

    public static LocalizationCatalog BuiltIn() => new(DefaultLanguage, ReadBuiltIn());

    public static LocalizationCatalog Load(string language)
    {
        var catalog = new LocalizationCatalog(language, ReadBuiltIn());
        var path = AppPaths.LocalizationFile(language);

        if (!File.Exists(path))
        {
            catalog.Warning = catalog.Format("startup.localizationMissing", language, path);
            return catalog;
        }

        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(path), ReadOptions);
            Flatten(document.RootElement, string.Empty, catalog._strings);
        }
        catch (Exception ex)
        {
            catalog.Warning = catalog.Format("startup.localizationUnreadable", path, ex.Message);
        }

        return catalog;
    }

    public string Text(string key) => _strings.TryGetValue(key, out var value) ? value : key;

    public string Format(string key, params object?[] arguments)
    {
        var template = Text(key);
        try
        {
            return string.Format(CultureInfo.InvariantCulture, template, arguments);
        }
        catch (FormatException)
        {
            return template;
        }
    }

    public string Plural(string key, int count, params object?[] arguments)
    {
        var form = $"{key}.{PluralRules.Form(Language, count)}";
        return Format(_strings.ContainsKey(form) ? form : $"{key}.{OtherForm}", arguments);
    }

    private static Dictionary<string, string> ReadBuiltIn()
    {
        using var stream = typeof(LocalizationCatalog).Assembly.GetManifestResourceStream(BuiltInResource)
                           ?? throw new InvalidOperationException($"The embedded resource {BuiltInResource} is missing.");
        using var document = JsonDocument.Parse(stream, ReadOptions);

        var strings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        Flatten(document.RootElement, string.Empty, strings);
        return strings;
    }

    private static void Flatten(JsonElement element, string prefix, Dictionary<string, string> target)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            return;
        }

        foreach (var property in element.EnumerateObject())
        {
            var key = prefix.Length == 0 ? property.Name : $"{prefix}.{property.Name}";
            switch (property.Value.ValueKind)
            {
                case JsonValueKind.Object:
                    Flatten(property.Value, key, target);
                    break;
                case JsonValueKind.String:
                    target[key] = property.Value.GetString() ?? string.Empty;
                    break;
            }
        }
    }
}
