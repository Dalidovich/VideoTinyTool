using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.Json;
using VideoTinyTool.Application;
using VideoTinyTool.Domain;

namespace VideoTinyTool.Media;

public sealed class ProbeFailedException : Exception
{
    public ProbeFailedException(string message) : base(message)
    {
    }
}

public static class MediaProbe
{
    public static MediaSource Probe(string path)
    {
        if (!File.Exists(path))
        {
            throw new ProbeFailedException("File not found.");
        }

        if (!FFmpegRuntime.FFprobeAvailable)
        {
            throw new ProbeFailedException($"ffprobe.exe was not found at {AppPaths.FFprobeExecutable}.");
        }

        var startInfo = new ProcessStartInfo
        {
            FileName = AppPaths.FFprobeExecutable,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8
        };

        startInfo.ArgumentList.Add("-v");
        startInfo.ArgumentList.Add("quiet");
        startInfo.ArgumentList.Add("-print_format");
        startInfo.ArgumentList.Add("json");
        startInfo.ArgumentList.Add("-show_format");
        startInfo.ArgumentList.Add("-show_streams");
        startInfo.ArgumentList.Add(path);

        using var process = Process.Start(startInfo)
                            ?? throw new ProbeFailedException("ffprobe could not be started.");

        var json = process.StandardOutput.ReadToEnd();
        var error = process.StandardError.ReadToEnd();
        process.WaitForExit();

        if (process.ExitCode != 0)
        {
            throw new ProbeFailedException(string.IsNullOrWhiteSpace(error)
                ? $"ffprobe exited with code {process.ExitCode}."
                : error.Trim());
        }

        return ParseProbeJson(json, path);
    }

    public static MediaSource ParseProbeJson(string json, string path)
    {
        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(json);
        }
        catch (JsonException ex)
        {
            throw new ProbeFailedException($"ffprobe returned unreadable output: {ex.Message}");
        }

        using (document)
        {
            var root = document.RootElement;
            if (!root.TryGetProperty("streams", out var streams) || streams.ValueKind != JsonValueKind.Array)
            {
                throw new ProbeFailedException("ffprobe reported no streams.");
            }

            JsonElement? video = null;
            JsonElement? audio = null;

            foreach (var stream in streams.EnumerateArray())
            {
                var type = ReadString(stream, "codec_type");
                if (video is null && type == "video" && !IsCoverArt(stream))
                {
                    video = stream;
                }
                else if (audio is null && type == "audio")
                {
                    audio = stream;
                }
            }

            if (video is null)
            {
                throw new ProbeFailedException("The file has no video stream.");
            }

            var videoStream = video.Value;
            var width = ReadInt(videoStream, "width") ?? 0;
            var height = ReadInt(videoStream, "height") ?? 0;
            if (width <= 0 || height <= 0)
            {
                throw new ProbeFailedException("The video stream has no usable frame size.");
            }

            var duration = ReadDuration(videoStream, root);
            if (duration <= TimeSpan.Zero)
            {
                throw new ProbeFailedException("The file has no readable duration.");
            }

            return new MediaSource(
                Guid.NewGuid(),
                path,
                duration,
                width,
                height,
                ParseFrameRate(ReadString(videoStream, "r_frame_rate")),
                ReadString(videoStream, "codec_name") ?? "unknown",
                audio is null ? null : ReadString(audio.Value, "codec_name") ?? "unknown");
        }
    }

    public static double ParseFrameRate(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return 25.0;
        }

        var parts = value.Split('/');
        if (parts.Length == 2
            && double.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var numerator)
            && double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var denominator)
            && denominator != 0
            && numerator > 0)
        {
            return numerator / denominator;
        }

        return double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var direct) && direct > 0
            ? direct
            : 25.0;
    }

    private static bool IsCoverArt(JsonElement stream)
    {
        if (!stream.TryGetProperty("disposition", out var disposition) || disposition.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        return disposition.TryGetProperty("attached_pic", out var attached)
               && attached.ValueKind == JsonValueKind.Number
               && attached.GetInt32() == 1;
    }

    private static TimeSpan ReadDuration(JsonElement videoStream, JsonElement root)
    {
        var streamDuration = ReadSeconds(videoStream, "duration");
        if (streamDuration is { } fromStream && fromStream > TimeSpan.Zero)
        {
            return fromStream;
        }

        if (root.TryGetProperty("format", out var format) && format.ValueKind == JsonValueKind.Object)
        {
            var formatDuration = ReadSeconds(format, "duration");
            if (formatDuration is { } fromFormat && fromFormat > TimeSpan.Zero)
            {
                return fromFormat;
            }
        }

        return TimeSpan.Zero;
    }

    private static TimeSpan? ReadSeconds(JsonElement element, string property)
    {
        var raw = ReadString(element, property);
        if (raw is null)
        {
            return null;
        }

        return double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var seconds)
            ? TimeSpan.FromSeconds(seconds)
            : null;
    }

    private static string? ReadString(JsonElement element, string property)
    {
        if (!element.TryGetProperty(property, out var value))
        {
            return null;
        }

        return value.ValueKind switch
        {
            JsonValueKind.String => value.GetString(),
            JsonValueKind.Number => value.GetRawText(),
            _ => null
        };
    }

    private static int? ReadInt(JsonElement element, string property)
    {
        if (!element.TryGetProperty(property, out var value))
        {
            return null;
        }

        return value.ValueKind switch
        {
            JsonValueKind.Number when value.TryGetInt32(out var number) => number,
            JsonValueKind.String when int.TryParse(value.GetString(), out var parsed) => parsed,
            _ => null
        };
    }
}
