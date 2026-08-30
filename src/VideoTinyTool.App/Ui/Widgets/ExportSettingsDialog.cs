using System.Globalization;
using SFML.Graphics;
using SFML.System;
using VideoTinyTool.Application;
using VideoTinyTool.Localization;

namespace VideoTinyTool.Ui.Widgets;

public sealed class ExportSettingsDialog : ModalDialog
{
    private const char ResolutionSeparator = '×';
    private const float RowHeight = 28f;
    private const float RowGap = 6f;
    private const float LabelGap = 18f;
    private const float PickerWidth = 190f;
    private const float TooltipWidth = 300f;
    private const float TooltipPadding = 9f;
    private const float TooltipLineHeight = 16f;
    private const float TooltipOffset = 16f;

    private static readonly string[] Containers = ["mp4", "mkv", "mov"];
    private static readonly string[] VideoCodecs = ["libx264", "libx265"];
    private static readonly string[] AudioCodecs = ["aac", "libmp3lame", "ac3"];

    private static readonly string[] Presets =
    [
        "ultrafast", "superfast", "veryfast", "faster", "fast", "medium", "slow", "slower", "veryslow"
    ];

    private static readonly double[] Speeds = [0.5, 0.75, 1, 1.25, 1.5, 1.75, 2, 2.5, 3];

    private static readonly int[] Qualities = [16, 18, 20, 23, 26, 30];
    private static readonly int[] FrameRates = [24, 25, 30, 50, 60];
    private static readonly int[] AudioBitrates = [96, 128, 160, 192, 256, 320];

    private static readonly (int Width, int Height)[] Resolutions =
    [
        (3840, 2160), (2560, 1440), (1920, 1080), (1280, 720), (854, 480)
    ];

    private readonly List<Row> _rows;

    private Vector2f _pointer;
    private Row? _hovered;

    public ExportSettingsDialog(ExportSettings export) : base(I18n.ExportSetup.Title, string.Empty)
    {
        _rows =
        [
            new Row(
                I18n.ExportSetup.Container,
                I18n.ExportSetup.ContainerHint,
                new OptionPicker(Options(Containers, export.Container), export.Container),
                (settings, value) => settings.Container = value),

            new Row(
                I18n.ExportSetup.VideoCodec,
                I18n.ExportSetup.VideoCodecHint,
                new OptionPicker(Options(VideoCodecs, export.VideoCodec), export.VideoCodec),
                (settings, value) => settings.VideoCodec = value),

            new Row(
                I18n.ExportSetup.Quality,
                I18n.ExportSetup.QualityHint,
                new OptionPicker(Options(Qualities, export.Crf), Number(export.Crf)),
                (settings, value) => settings.Crf = Number(value)),

            new Row(
                I18n.ExportSetup.Preset,
                I18n.ExportSetup.PresetHint,
                new OptionPicker(Options(Presets, export.Preset), export.Preset),
                (settings, value) => settings.Preset = value),

            new Row(
                I18n.ExportSetup.Resolution,
                I18n.ExportSetup.ResolutionHint,
                new OptionPicker(
                    ResolutionOptions(export.Width, export.Height),
                    Resolution(export.Width, export.Height)),
                ApplyResolution),

            new Row(
                I18n.ExportSetup.FrameRate,
                I18n.ExportSetup.FrameRateHint,
                new OptionPicker(Options(FrameRates, export.FrameRate), Number(export.FrameRate)),
                (settings, value) => settings.FrameRate = Number(value)),

            new Row(
                I18n.ExportSetup.Speed,
                I18n.ExportSetup.SpeedHint,
                new OptionPicker(Options(Speeds, export.Speed), Number(export.Speed)),
                (settings, value) => settings.Speed = Fraction(value)),

            new Row(
                I18n.ExportSetup.AudioCodec,
                I18n.ExportSetup.AudioCodecHint,
                new OptionPicker(Options(AudioCodecs, export.AudioCodec), export.AudioCodec),
                (settings, value) => settings.AudioCodec = value),

            new Row(
                I18n.ExportSetup.AudioBitrate,
                I18n.ExportSetup.AudioBitrateHint,
                new OptionPicker(Options(AudioBitrates, export.AudioBitrateKbps), Number(export.AudioBitrateKbps)),
                (settings, value) => settings.AudioBitrateKbps = Number(value))
        ];
    }

    public void Apply(ExportSettings export)
    {
        foreach (var row in _rows)
        {
            row.Apply(export, row.Picker.Value);
        }
    }

    public override float ContentHeight(Renderer renderer, float contentWidth) =>
        (_rows.Count * (RowHeight + RowGap)) - RowGap;

    protected override float MeasureWidth(Renderer renderer) =>
        Math.Max(
            base.MeasureWidth(renderer),
            MathF.Round(LabelWidth(renderer) + LabelGap + PickerWidth + (HorizontalPadding * 2)));

    public override void Layout(Renderer renderer, uint windowWidth, uint windowHeight)
    {
        base.Layout(renderer, windowWidth, windowHeight);

        var left = Bounds.Left + HorizontalPadding;
        var width = Bounds.Width - (HorizontalPadding * 2);
        var top = ContentTop;

        foreach (var row in _rows)
        {
            row.Bounds = new FloatRect(new Vector2f(left, top), new Vector2f(width, RowHeight));
            row.Picker.Bounds = new FloatRect(
                new Vector2f(left + width - PickerWidth, MathF.Round(top + 1f)),
                new Vector2f(PickerWidth, RowHeight - 2f));
            row.Picker.Layout();

            top += RowHeight + RowGap;
        }
    }

    public override void UpdateHover(Vector2f point)
    {
        base.UpdateHover(point);

        _pointer = point;
        _hovered = _rows.FirstOrDefault(row => row.Bounds.Contains(point));

        foreach (var row in _rows)
        {
            row.Picker.UpdateHover(point);
        }
    }

    public override void OnMouseDown(Vector2f point)
    {
        base.OnMouseDown(point);

        foreach (var row in _rows)
        {
            row.Picker.OnMouseDown(point);
        }
    }

    public override void OnMouseUp(Vector2f point)
    {
        base.OnMouseUp(point);

        foreach (var row in _rows)
        {
            row.Picker.OnMouseUp(point);
        }
    }

    public override void Draw(Renderer renderer, uint windowWidth, uint windowHeight)
    {
        base.Draw(renderer, windowWidth, windowHeight);
        DrawTooltip(renderer, windowWidth, windowHeight);
    }

    protected override void DrawContent(Renderer renderer, float left, float top, float width)
    {
        foreach (var row in _rows)
        {
            var hovered = ReferenceEquals(row, _hovered);
            if (hovered)
            {
                renderer.FillRect(row.Bounds, Theme.RowHover);
            }

            var labelWidth = row.Picker.Bounds.Left - row.Bounds.Left - LabelGap;
            renderer.DrawText(
                renderer.Ellipsize(row.Label, labelWidth, Theme.FontSizeBody),
                row.Bounds.Left,
                row.Bounds.Top + 6f,
                Theme.FontSizeBody,
                hovered ? Theme.Text : Theme.TextDim);

            row.Picker.Draw(renderer);
        }
    }

    private void DrawTooltip(Renderer renderer, uint windowWidth, uint windowHeight)
    {
        if (_hovered is null)
        {
            return;
        }

        var lines = WrapText(renderer, _hovered.Hint, TooltipWidth, Theme.FontSizeSmall);
        var textWidth = lines.Max(line => renderer.MeasureText(line, Theme.FontSizeSmall));
        var width = MathF.Ceiling(textWidth + (TooltipPadding * 2));
        var height = MathF.Ceiling((lines.Count * TooltipLineHeight) + (TooltipPadding * 2));

        var x = Math.Clamp(_pointer.X + TooltipOffset, 4f, Math.Max(4f, windowWidth - width - 4f));
        var y = _pointer.Y + TooltipOffset;
        if (y + height > windowHeight - 4f)
        {
            y = Math.Max(4f, _pointer.Y - TooltipOffset - height);
        }

        var bounds = new FloatRect(
            new Vector2f(MathF.Round(x), MathF.Round(y)),
            new Vector2f(width, height));

        renderer.FillAndStroke(bounds, Theme.Chrome, Theme.LineStrong);

        var textTop = bounds.Top + TooltipPadding;
        foreach (var line in lines)
        {
            renderer.DrawText(line, bounds.Left + TooltipPadding, textTop, Theme.FontSizeSmall, Theme.Text);
            textTop += TooltipLineHeight;
        }
    }

    private float LabelWidth(Renderer renderer) =>
        _rows.Max(row => MathF.Ceiling(renderer.MeasureText(row.Label, Theme.FontSizeBody)));

    private static void ApplyResolution(ExportSettings settings, string value)
    {
        var parts = value.Split(ResolutionSeparator);
        if (parts.Length != 2)
        {
            return;
        }

        settings.Width = Number(parts[0]);
        settings.Height = Number(parts[1]);
    }

    private static IReadOnlyList<string> Options(IReadOnlyList<string> known, string current)
    {
        if (known.Contains(current, StringComparer.OrdinalIgnoreCase))
        {
            return known;
        }

        return [.. known, current];
    }

    private static IReadOnlyList<string> Options(IReadOnlyList<int> known, int current) =>
        known.Append(current).Distinct().Order().Select(Number).ToArray();

    private static IReadOnlyList<string> Options(IReadOnlyList<double> known, double current) =>
        known.Append(ExportSettings.ClampSpeed(current)).Distinct().Order().Select(Number).ToArray();

    private static IReadOnlyList<string> ResolutionOptions(int width, int height) =>
        Options(
            Resolutions.Select(size => Resolution(size.Width, size.Height)).ToArray(),
            Resolution(width, height));

    private static string Resolution(int width, int height) =>
        Number(width) + ResolutionSeparator + Number(height);

    private static string Number(int value) => value.ToString(CultureInfo.InvariantCulture);

    private static string Number(double value) =>
        ExportSettings.ClampSpeed(value).ToString("0.###", CultureInfo.InvariantCulture);

    private static int Number(string value) => int.Parse(value, CultureInfo.InvariantCulture);

    private static double Fraction(string value) => double.Parse(value, CultureInfo.InvariantCulture);

    private sealed class Row
    {
        public Row(string label, string hint, OptionPicker picker, Action<ExportSettings, string> apply)
        {
            Label = label;
            Hint = hint;
            Picker = picker;
            Apply = apply;
        }

        public string Label { get; }

        public string Hint { get; }

        public OptionPicker Picker { get; }

        public Action<ExportSettings, string> Apply { get; }

        public FloatRect Bounds { get; set; }
    }
}
