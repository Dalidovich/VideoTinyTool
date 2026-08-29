using System.Text.Json;

namespace VideoTinyTool.Application;

public static class SettingsLoader
{
    private static readonly JsonSerializerOptions ReadOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    private static readonly JsonSerializerOptions WriteOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public static AppSettings LoadOrCreate(string path)
    {
        if (!File.Exists(path))
        {
            var created = new AppSettings();
            TryWrite(path, created, out var writeError);
            created.LoadWarning = writeError;
            created.Sanitize();
            return created;
        }

        try
        {
            var json = File.ReadAllText(path);
            var parsed = JsonSerializer.Deserialize<AppSettings>(json, ReadOptions) ?? new AppSettings();
            parsed.Export ??= new ExportSettings();
            parsed.Preview ??= new PreviewSettings();
            parsed.Window ??= new WindowSettings();
            parsed.Sanitize();
            return parsed;
        }
        catch (Exception ex)
        {
            var defaults = new AppSettings
            {
                LoadWarning = $"settings.json could not be read, defaults are used.\n{ex.Message}"
            };
            defaults.Sanitize();
            return defaults;
        }
    }

    private static void TryWrite(string path, AppSettings settings, out string? error)
    {
        try
        {
            File.WriteAllText(path, JsonSerializer.Serialize(settings, WriteOptions));
            error = null;
        }
        catch (Exception ex)
        {
            error = $"settings.json could not be created next to the executable.\n{ex.Message}";
        }
    }
}
