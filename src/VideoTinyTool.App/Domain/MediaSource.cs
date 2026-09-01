namespace VideoTinyTool.Domain;

public sealed class MediaSource
{
    public MediaSource(
        Guid id,
        string path,
        TimeSpan duration,
        int width,
        int height,
        double frameRate,
        string? videoCodec,
        string? audioCodec)
    {
        Id = id;
        Path = path;
        FileName = System.IO.Path.GetFileName(path);
        Duration = duration;
        Width = width;
        Height = height;
        FrameRate = frameRate > 0 ? frameRate : 25.0;
        VideoCodec = videoCodec;
        AudioCodec = audioCodec;
    }

    public Guid Id { get; }
    public string Path { get; }
    public string FileName { get; }
    public TimeSpan Duration { get; }
    public int Width { get; }
    public int Height { get; }
    public double FrameRate { get; }
    public string? VideoCodec { get; }
    public string? AudioCodec { get; }

    public bool HasVideo => VideoCodec is not null;

    public bool HasAudio => AudioCodec is not null;

    public TimeSpan FrameDuration => TimeSpan.FromSeconds(1.0 / FrameRate);

    public double AspectRatio => Width > 0 && Height > 0 ? (double)Width / Height : 16.0 / 9.0;
}
