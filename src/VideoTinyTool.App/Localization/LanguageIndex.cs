using VideoTinyTool.Application;

namespace VideoTinyTool.Localization;

public readonly record struct LanguageOption(string Code, string Name)
{
    public string Badge => Code.ToUpperInvariant();
}

public static class LanguageIndex
{
    private const int MaxCodeLength = 16;

    public static IReadOnlyList<LanguageOption> Scan() => Scan(AppPaths.LocalizationDirectory);

    public static IReadOnlyList<LanguageOption> Scan(string directory)
    {
        var found = new SortedDictionary<string, LanguageOption>(StringComparer.Ordinal);

        if (Directory.Exists(directory))
        {
            foreach (var path in Directory.EnumerateFiles(directory, "*.json"))
            {
                var code = Normalize(Path.GetFileNameWithoutExtension(path));
                if (code is null)
                {
                    continue;
                }

                found[code] = new LanguageOption(code, ReadName(path, code));
            }
        }

        found.TryAdd(
            LocalizationCatalog.DefaultLanguage,
            new LanguageOption(LocalizationCatalog.DefaultLanguage, LocalizationCatalog.DefaultLanguage.ToUpperInvariant()));

        return [.. found.Values];
    }

    public static string? Normalize(string? value)
    {
        var code = (value ?? string.Empty).Trim().ToLowerInvariant();
        var usable = code.Length is > 0 and <= MaxCodeLength
                     && code.All(character => char.IsAsciiLetterOrDigit(character) || character == '-');

        return usable ? code : null;
    }

    private static string ReadName(string path, string code)
    {
        try
        {
            return LocalizationCatalog.ReadFile(path).TryGetValue(LocalizationCatalog.NameKey, out var name)
                   && !string.IsNullOrWhiteSpace(name)
                ? name.Trim()
                : code.ToUpperInvariant();
        }
        catch (Exception)
        {
            return code.ToUpperInvariant();
        }
    }
}
