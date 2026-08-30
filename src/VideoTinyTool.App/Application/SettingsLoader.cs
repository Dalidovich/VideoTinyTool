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
            created.LoadIssue = writeError is null
                ? null
                : new SettingsLoadIssue(SettingsLoadFailure.NotCreated, writeError);
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
            parsed.Ui ??= new UiSettings();
            parsed.Sanitize();
            return parsed;
        }
        catch (Exception ex)
        {
            var defaults = new AppSettings
            {
                LoadIssue = new SettingsLoadIssue(SettingsLoadFailure.Unreadable, ex.Message)
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
            error = ex.Message;
        }
    }
}
