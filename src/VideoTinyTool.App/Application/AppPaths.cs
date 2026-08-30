namespace VideoTinyTool.Application;

public static class AppPaths
{
    public static string BaseDirectory { get; } = AppContext.BaseDirectory;

    public static string FFmpegDirectory => BaseDirectory;

    public static string FFmpegExecutable => Path.Combine(BaseDirectory, "ffmpeg.exe");

    public static string FFprobeExecutable => Path.Combine(BaseDirectory, "ffprobe.exe");

    public static string SettingsFile => Path.Combine(BaseDirectory, "settings.json");

    public static string AssetsDirectory => Path.Combine(BaseDirectory, "assets");

    public static string FontsDirectory => Path.Combine(AssetsDirectory, "fonts");

    public static string LocalizationDirectory => Path.Combine(AssetsDirectory, "localization");

    public static string NativeDirectory => Path.Combine(BaseDirectory, "runtime");

    public static string Font(string fileName) => Path.Combine(FontsDirectory, fileName);

    public static string LocalizationFile(string language) =>
        Path.Combine(LocalizationDirectory, language + ".json");
}
