using System.Text.Json;
using System.Text.Json.Nodes;

namespace VideoTinyTool.Application;

public static class SettingsLoader
{
    private static readonly JsonSerializerOptions ReadOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    private static readonly JsonDocumentOptions NodeReadOptions = new()
    {
        CommentHandling = JsonCommentHandling.Skip,
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

    public static string? TrySaveLanguage(string path, string language)
    {
        try
        {
            var root = File.Exists(path)
                ? JsonNode.Parse(File.ReadAllText(path), null, NodeReadOptions) as JsonObject ?? new JsonObject()
                : new JsonObject();

            Set(Section(root, "ui"), "language", language);
            File.WriteAllText(path, root.ToJsonString(WriteOptions));
            return null;
        }
        catch (Exception ex)
        {
            return ex.Message;
        }
    }

    private static JsonObject Section(JsonObject root, string name)
    {
        if (Key(root, name) is { } existing && root[existing] is JsonObject section)
        {
            return section;
        }

        var created = new JsonObject();
        root[Key(root, name) ?? name] = created;
        return created;
    }

    private static void Set(JsonObject target, string name, string value) => target[Key(target, name) ?? name] = value;

    private static string? Key(JsonObject target, string name) => target
        .Select(property => property.Key)
        .FirstOrDefault(key => string.Equals(key, name, StringComparison.OrdinalIgnoreCase));

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
