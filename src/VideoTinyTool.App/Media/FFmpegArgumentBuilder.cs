using System.Globalization;
using System.Text;
using VideoTinyTool.Application;
using VideoTinyTool.Domain;

namespace VideoTinyTool.Media;

public readonly record struct ExportItem(string SourcePath, TimeSpan In, TimeSpan Out, bool HasAudio)
{
    public static ExportItem Gap(TimeSpan duration) => new(string.Empty, TimeSpan.Zero, duration, false);

    public bool IsGap => SourcePath.Length == 0;

    public TimeSpan Duration => Out - In;
}

public readonly record struct OverlayItem(
    string SourcePath,
    TimeSpan In,
    TimeSpan Out,
    bool HasAudio,
    TimeSpan Start,
    OverlayTransform Transform,
    ClipAudio Audio)
{
    public TimeSpan Duration => Out - In;

    public bool HasSound => HasAudio && Audio.Gain > 0f;
}

public static class FFmpegArgumentBuilder
{
    private const string SpeedVideoLabel = "[vspeed]";
    private const string SpeedAudioLabel = "[aspeed]";
    private const double MaxTempoStep = 2.0;
    private const double SpeedTolerance = 0.0005;

    public static IReadOnlyList<ExportItem> BuildItems(Timeline timeline, IReadOnlyDictionary<Guid, MediaSource> sources)
    {
        var items = new List<ExportItem>(timeline.Clips.Count);
        foreach (var clip in timeline.Clips)
        {
            if (!sources.TryGetValue(clip.SourceId, out var source))
            {
                continue;
            }

            if (clip.LeadingGap > TimeSpan.Zero)
            {
                items.Add(ExportItem.Gap(clip.LeadingGap));
            }

            items.Add(new ExportItem(source.Path, clip.In, clip.Out, source.HasAudio));
        }

        return items;
    }

    public static IReadOnlyList<OverlayItem> BuildOverlayItems(Timeline timeline, IReadOnlyDictionary<Guid, MediaSource> sources)
    {
        var items = new List<OverlayItem>();

        for (var trackIndex = 1; trackIndex < timeline.VideoTrackCount; trackIndex++)
        {
            var start = TimeSpan.Zero;
            foreach (var clip in timeline.Tracks[trackIndex].Clips)
            {
                start += clip.LeadingGap;

                if (sources.TryGetValue(clip.SourceId, out var source))
                {
                    items.Add(new OverlayItem(
                        source.Path,
                        clip.In,
                        clip.Out,
                        source.HasAudio,
                        start,
                        clip.Overlay,
                        clip.Audio));
                }

                start += clip.Duration;
            }
        }

        return items;
    }

    public static string Build(IReadOnlyList<ExportItem> items, ExportSettings export, string outputPath) =>
        Build(items, [], export, outputPath);

    public static string Build(
        IReadOnlyList<ExportItem> items,
        IReadOnlyList<OverlayItem> overlays,
        ExportSettings export,
        string outputPath)
    {
        if (items.Count == 0)
        {
            throw new ArgumentException("Export needs at least one clip.", nameof(items));
        }

        var args = new StringBuilder();
        args.Append("-y -hide_banner -stats");

        foreach (var item in items)
        {
            if (item.IsGap)
            {
                continue;
            }

            args.Append(" -ss ").Append(Seconds(item.In));
            args.Append(" -t ").Append(Seconds(item.Duration));
            args.Append(" -i ").Append(Quote(item.SourcePath));
        }

        foreach (var overlay in overlays)
        {
            args.Append(" -ss ").Append(Seconds(overlay.In));
            args.Append(" -t ").Append(Seconds(overlay.Duration));
            args.Append(" -i ").Append(Quote(overlay.SourcePath));
        }

        args.Append(" -filter_complex ").Append(Quote(BuildFilterGraph(items, overlays, export)));
        args.Append(" -map ").Append(Quote(VideoLabel(overlays, export)));
        args.Append(" -map ").Append(Quote(AudioLabel(overlays, export)));
        args.Append(" -c:v ").Append(export.VideoCodec);
        args.Append(" -crf ").Append(export.Crf.ToString(CultureInfo.InvariantCulture));
        args.Append(" -preset ").Append(export.Preset);
        args.Append(" -c:a ").Append(export.AudioCodec);
        args.Append(" -b:a ").Append(export.AudioBitrateKbps.ToString(CultureInfo.InvariantCulture)).Append('k');
        args.Append(" -movflags +faststart");
        args.Append(' ').Append(Quote(outputPath));

        return args.ToString();
    }

    public static string BuildFilterGraph(IReadOnlyList<ExportItem> items, ExportSettings export) =>
        BuildFilterGraph(items, [], export);

    public static string BuildFilterGraph(
        IReadOnlyList<ExportItem> items,
        IReadOnlyList<OverlayItem> overlays,
        ExportSettings export)
    {
        var width = export.Width.ToString(CultureInfo.InvariantCulture);
        var height = export.Height.ToString(CultureInfo.InvariantCulture);
        var fps = export.FrameRate.ToString(CultureInfo.InvariantCulture);

        var graph = new StringBuilder();
        var concat = new StringBuilder();

        var input = 0;

        for (var i = 0; i < items.Count; i++)
        {
            var item = items[i];

            if (item.IsGap)
            {
                graph.Append("color=c=black:s=").Append(width).Append('x').Append(height)
                     .Append(":r=").Append(fps).Append(":d=").Append(Seconds(item.Duration));
                graph.Append(",setsar=1,format=yuv420p");
            }
            else
            {
                graph.Append('[').Append(input).Append(":v]");
                graph.Append("scale=").Append(width).Append(':').Append(height)
                     .Append(":force_original_aspect_ratio=decrease,");
                graph.Append("pad=").Append(width).Append(':').Append(height).Append(":(ow-iw)/2:(oh-ih)/2,");
                graph.Append("setsar=1,fps=").Append(fps).Append(",format=yuv420p");
            }

            graph.Append("[v").Append(i).Append("];");

            if (!item.IsGap && item.HasAudio)
            {
                graph.Append('[').Append(input).Append(":a]");
                graph.Append("aresample=48000,aformat=sample_fmts=fltp:channel_layouts=stereo");
            }
            else
            {
                graph.Append("anullsrc=channel_layout=stereo:sample_rate=48000:d=")
                     .Append(Seconds(item.Duration))
                     .Append(",aformat=sample_fmts=fltp:channel_layouts=stereo");
            }

            graph.Append("[a").Append(i).Append("];");

            concat.Append("[v").Append(i).Append("][a").Append(i).Append(']');

            if (!item.IsGap)
            {
                input++;
            }
        }

        graph.Append(concat);
        graph.Append("concat=n=").Append(items.Count.ToString(CultureInfo.InvariantCulture)).Append(":v=1:a=1[v][a]");

        AppendOverlays(graph, items, overlays, export);
        AppendSpeed(graph, overlays, export);

        return graph.ToString();
    }

    public static TimeSpan OutputDuration(TimeSpan timelineDuration, ExportSettings export) =>
        timelineDuration / ExportSettings.ClampSpeed(export.Speed);

    private static void AppendOverlays(
        StringBuilder graph,
        IReadOnlyList<ExportItem> items,
        IReadOnlyList<OverlayItem> overlays,
        ExportSettings export)
    {
        if (overlays.Count == 0)
        {
            return;
        }

        var fps = export.FrameRate.ToString(CultureInfo.InvariantCulture);
        var input = 0;
        foreach (var item in items)
        {
            if (!item.IsGap)
            {
                input++;
            }
        }

        var accumulated = "[v]";

        for (var k = 0; k < overlays.Count; k++)
        {
            var transform = overlays[k].Transform;

            graph.Append(";[").Append(input + k).Append(":v]");
            graph.Append("scale=").Append(EvenWidth(export.Width, transform.Width)).Append(":-2,");
            graph.Append("setsar=1,fps=").Append(fps).Append(",format=yuva420p,");
            graph.Append("colorchannelmixer=aa=").Append(Number(transform.Opacity)).Append(',');
            graph.Append("setpts=PTS-STARTPTS+").Append(Seconds(overlays[k].Start)).Append("/TB");
            graph.Append("[ov").Append(k).Append("];");

            graph.Append(accumulated).Append("[ov").Append(k).Append(']');
            graph.Append("overlay=x=").Append(Coordinate(export.Width, transform.X));
            graph.Append(":y=").Append(Coordinate(export.Height, transform.Y));
            graph.Append(":eof_action=pass:shortest=0");
            graph.Append("[acc").Append(k).Append(']');

            accumulated = "[acc" + k.ToString(CultureInfo.InvariantCulture) + "]";
        }

        var mix = new StringBuilder();
        var mixInputs = 1;

        for (var k = 0; k < overlays.Count; k++)
        {
            if (!overlays[k].HasSound)
            {
                continue;
            }

            var delay = Milliseconds(overlays[k].Start);

            graph.Append(";[").Append(input + k).Append(":a]");
            graph.Append("aresample=48000,aformat=sample_fmts=fltp:channel_layouts=stereo,");
            graph.Append("volume=").Append(Number(overlays[k].Audio.Gain)).Append(',');
            graph.Append("adelay=").Append(delay).Append('|').Append(delay);
            graph.Append("[oa").Append(k).Append(']');

            mix.Append("[oa").Append(k).Append(']');
            mixInputs++;
        }

        if (mixInputs == 1)
        {
            return;
        }

        graph.Append(";[a]").Append(mix);
        graph.Append("amix=inputs=").Append(mixInputs.ToString(CultureInfo.InvariantCulture));
        graph.Append(":normalize=0:dropout_transition=0[amixed]");
    }

    private static void AppendSpeed(StringBuilder graph, IReadOnlyList<OverlayItem> overlays, ExportSettings export)
    {
        var speed = ExportSettings.ClampSpeed(export.Speed);
        if (!ChangesSpeed(speed))
        {
            return;
        }

        graph.Append(';').Append(ComposedVideoLabel(overlays));
        graph.Append("setpts=PTS/").Append(Number(speed));
        graph.Append(",fps=").Append(export.FrameRate.ToString(CultureInfo.InvariantCulture));
        graph.Append(SpeedVideoLabel);

        graph.Append(';').Append(ComposedAudioLabel(overlays));
        graph.Append(Tempo(speed));
        graph.Append(SpeedAudioLabel);
    }

    private static string Tempo(double speed) =>
        speed <= MaxTempoStep
            ? "atempo=" + Number(speed)
            : "atempo=" + Number(MaxTempoStep) + ",atempo=" + Number(speed / MaxTempoStep);

    private static bool ChangesSpeed(double speed) =>
        Math.Abs(speed - ExportSettings.NormalSpeed) > SpeedTolerance;

    private static string VideoLabel(IReadOnlyList<OverlayItem> overlays, ExportSettings export) =>
        ChangesSpeed(ExportSettings.ClampSpeed(export.Speed))
            ? SpeedVideoLabel
            : ComposedVideoLabel(overlays);

    private static string AudioLabel(IReadOnlyList<OverlayItem> overlays, ExportSettings export) =>
        ChangesSpeed(ExportSettings.ClampSpeed(export.Speed))
            ? SpeedAudioLabel
            : ComposedAudioLabel(overlays);

    private static string ComposedVideoLabel(IReadOnlyList<OverlayItem> overlays) =>
        overlays.Count == 0
            ? "[v]"
            : "[acc" + (overlays.Count - 1).ToString(CultureInfo.InvariantCulture) + "]";

    private static string ComposedAudioLabel(IReadOnlyList<OverlayItem> overlays)
    {
        foreach (var overlay in overlays)
        {
            if (overlay.HasSound)
            {
                return "[amixed]";
            }
        }

        return "[a]";
    }

    private static string EvenWidth(int exportWidth, float fraction)
    {
        var width = (int)Math.Round(exportWidth * (double)fraction, MidpointRounding.AwayFromZero);
        if (width % 2 != 0)
        {
            width++;
        }

        return Math.Max(2, width).ToString(CultureInfo.InvariantCulture);
    }

    private static string Coordinate(int extent, float fraction) =>
        ((int)Math.Round(extent * (double)fraction, MidpointRounding.AwayFromZero))
            .ToString(CultureInfo.InvariantCulture);

    private static string Milliseconds(TimeSpan value) =>
        ((long)Math.Round(value.TotalMilliseconds, MidpointRounding.AwayFromZero))
            .ToString(CultureInfo.InvariantCulture);

    private static string Number(double value) => value.ToString("0.###", CultureInfo.InvariantCulture);

    public static string Seconds(TimeSpan value) =>
        value.TotalSeconds.ToString("0.###", CultureInfo.InvariantCulture);

    private static string Quote(string value) => "\"" + value.Replace("\"", "\\\"") + "\"";
}
