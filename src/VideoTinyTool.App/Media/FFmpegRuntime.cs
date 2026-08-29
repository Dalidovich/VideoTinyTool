using NReco.VideoConverter;
using VideoTinyTool.Application;

namespace VideoTinyTool.Media;

public static class FFmpegRuntime
{
    public static bool FFmpegAvailable => File.Exists(AppPaths.FFmpegExecutable);

    public static bool FFprobeAvailable => File.Exists(AppPaths.FFprobeExecutable);

    public static bool Available => FFmpegAvailable && FFprobeAvailable;

    public static string MissingBinariesMessage
    {
        get
        {
            var missing = new List<string>();
            if (!FFmpegAvailable)
            {
                missing.Add(AppPaths.FFmpegExecutable);
            }

            if (!FFprobeAvailable)
            {
                missing.Add(AppPaths.FFprobeExecutable);
            }

            return "Required binaries were not found:\n" + string.Join("\n", missing);
        }
    }

    public static FFMpegConverter CreateConverter() => new()
    {
        FFMpegToolPath = AppPaths.FFmpegDirectory
    };
}
