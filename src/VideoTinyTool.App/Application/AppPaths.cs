namespace VideoTinyTool.Application;

public static class AppPaths
{
    public static string BaseDirectory { get; } = AppContext.BaseDirectory;

    public static string FFmpegDirectory => BaseDirectory;

    public static string FFmpegExecutable => Path.Combine(BaseDirectory, "ffmpeg.exe");

    public static string FFprobeExecutable => Path.Combine(BaseDirectory, "ffprobe.exe");

    public static string SettingsFile => Path.Combine(BaseDirectory, "settings.json");

    public static string FontsDirectory => Path.Combine(BaseDirectory, "fonts");

    public static string NativeDirectory => Path.Combine(BaseDirectory, "runtime");

    public static string Font(string fileName) => Path.Combine(FontsDirectory, fileName);
}
