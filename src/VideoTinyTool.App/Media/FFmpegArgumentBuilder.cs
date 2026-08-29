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

public static class FFmpegArgumentBuilder
{
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

    public static string Build(IReadOnlyList<ExportItem> items, ExportSettings export, string outputPath)
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

        args.Append(" -filter_complex ").Append(Quote(BuildFilterGraph(items, export)));
        args.Append(" -map \"[v]\" -map \"[a]\"");
        args.Append(" -c:v ").Append(export.VideoCodec);
        args.Append(" -crf ").Append(export.Crf.ToString(CultureInfo.InvariantCulture));
        args.Append(" -preset ").Append(export.Preset);
        args.Append(" -c:a ").Append(export.AudioCodec);
        args.Append(" -b:a ").Append(export.AudioBitrateKbps.ToString(CultureInfo.InvariantCulture)).Append('k');
        args.Append(" -movflags +faststart");
        args.Append(' ').Append(Quote(outputPath));

        return args.ToString();
    }

    public static string BuildFilterGraph(IReadOnlyList<ExportItem> items, ExportSettings export)
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

        return graph.ToString();
    }

    public static string Seconds(TimeSpan value) =>
        value.TotalSeconds.ToString("0.###", CultureInfo.InvariantCulture);

    private static string Quote(string value) => "\"" + value.Replace("\"", "\\\"") + "\"";
}
