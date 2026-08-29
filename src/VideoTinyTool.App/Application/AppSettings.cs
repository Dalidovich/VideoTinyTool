using System.Text.Json.Serialization;

namespace VideoTinyTool.Application;

public sealed class ExportSettings
{
    public string Container { get; set; } = "mp4";
    public string VideoCodec { get; set; } = "libx264";
    public int Crf { get; set; } = 20;
    public string Preset { get; set; } = "medium";
    public int Width { get; set; } = 1920;
    public int Height { get; set; } = 1080;
    public int FrameRate { get; set; } = 30;
    public string AudioCodec { get; set; } = "aac";
    public int AudioBitrateKbps { get; set; } = 192;
}

public sealed class PreviewSettings
{
    public int Width { get; set; } = 960;
    public int Height { get; set; } = 540;
}

public sealed class WindowSettings
{
    public int Width { get; set; } = 1600;
    public int Height { get; set; } = 900;
}

public sealed class AppSettings
{
    public ExportSettings Export { get; set; } = new();
    public PreviewSettings Preview { get; set; } = new();
    public WindowSettings Window { get; set; } = new();

    [JsonIgnore]
    public string? LoadWarning { get; set; }

    public void Sanitize()
    {
        Export.Container = Fallback(Export.Container, "mp4").TrimStart('.');
        Export.VideoCodec = Fallback(Export.VideoCodec, "libx264");
        Export.Preset = Fallback(Export.Preset, "medium");
        Export.AudioCodec = Fallback(Export.AudioCodec, "aac");
        Export.Crf = Math.Clamp(Export.Crf, 0, 51);
        Export.Width = EvenClamp(Export.Width, 1920, 16, 7680);
        Export.Height = EvenClamp(Export.Height, 1080, 16, 4320);
        Export.FrameRate = Math.Clamp(Export.FrameRate <= 0 ? 30 : Export.FrameRate, 1, 240);
        Export.AudioBitrateKbps = Math.Clamp(Export.AudioBitrateKbps <= 0 ? 192 : Export.AudioBitrateKbps, 32, 512);

        Preview.Width = EvenClamp(Preview.Width, 960, 160, 3840);
        Preview.Height = EvenClamp(Preview.Height, 540, 90, 2160);

        Window.Width = Math.Clamp(Window.Width <= 0 ? 1600 : Window.Width, 1100, 7680);
        Window.Height = Math.Clamp(Window.Height <= 0 ? 900 : Window.Height, 700, 4320);
    }

    private static string Fallback(string? value, string standard) =>
        string.IsNullOrWhiteSpace(value) ? standard : value.Trim();

    private static int EvenClamp(int value, int standard, int min, int max)
    {
        var result = value <= 0 ? standard : value;
        result = Math.Clamp(result, min, max);
        return result % 2 == 0 ? result : result - 1;
    }
}
