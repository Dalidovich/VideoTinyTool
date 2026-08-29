using System.Globalization;
using SFML.Graphics;
using SFML.System;
using SFML.Window;
using VideoTinyTool.Ui.Widgets;

namespace VideoTinyTool.Ui.Panels;

public sealed class PreviewPanel : PanelBase
{
    private const float TransportPadding = 10f;
    private const float IconWidth = 26f;
    private const float IconHeight = 20f;

    private readonly IEditorHost _host;
    private readonly Button _toStart = new("|◀");
    private readonly Button _playPause = new("▶", ButtonStyle.Accent);
    private readonly Button _toEnd = new("▶|");
    private readonly Slider _scrub = new();

    private bool _wasPlayingBeforeScrub;

    public PreviewPanel(IEditorHost host)
    {
        _host = host;

        _toStart.Clicked += () => _host.SeekTo(TimeSpan.Zero, false);
        _toEnd.Clicked += () => _host.SeekTo(_host.Timeline.TotalDuration, false);
        _playPause.Clicked += () => _host.Player.TogglePlay();

        _scrub.ValueChanged += value =>
            _host.SeekTo(TimeSpan.FromTicks((long)(_host.Timeline.TotalDuration.Ticks * value)), true);
        _scrub.DragFinished += () => _host.EndScrub(_wasPlayingBeforeScrub);
    }

    private IEnumerable<Button> Buttons
    {
        get
        {
            yield return _toStart;
            yield return _playPause;
            yield return _toEnd;
        }
    }

    private FloatRect TransportBounds => new(
        new Vector2f(Bounds.Left, Bounds.Top + Bounds.Height - Theme.TransportHeight),
        new Vector2f(Bounds.Width, Theme.TransportHeight));

    private FloatRect FrameBounds => new(
        new Vector2f(Bounds.Left, Bounds.Top + Theme.PanelHeaderHeight),
        new Vector2f(Bounds.Width, Bounds.Height - Theme.PanelHeaderHeight - Theme.TransportHeight));

    public void Layout(Renderer renderer)
    {
        var transport = TransportBounds;
        var top = transport.Top + ((transport.Height - IconHeight) / 2f);
        var x = transport.Left + TransportPadding;

        foreach (var button in Buttons)
        {
            button.Bounds = new FloatRect(new Vector2f(MathF.Round(x), MathF.Round(top)), new Vector2f(IconWidth, IconHeight));
            x += IconWidth + 4f;
        }

        var timecode = TimecodeText();
        var timecodeWidth = renderer.MeasureText(timecode, Theme.FontSizeLabel, TextFont.Mono) + 14f;

        var scrubLeft = x + 8f;
        var scrubRight = transport.Left + transport.Width - TransportPadding - timecodeWidth;

        _scrub.Bounds = new FloatRect(
            new Vector2f(scrubLeft, transport.Top + ((transport.Height - 10f) / 2f)),
            new Vector2f(Math.Max(30f, scrubRight - scrubLeft), 10f));
    }

    private string TimecodeText() =>
        $"{TimeFormat.Timecode(_host.Player.Position)} / {TimeFormat.Timecode(_host.Timeline.TotalDuration)}";

    public override void Draw(Renderer renderer)
    {
        renderer.FillRect(Bounds, Theme.Sunk);

        var meta = string.Format(
            CultureInfo.InvariantCulture,
            "{0}×{1} · {2} fps",
            _host.Settings.Export.Width,
            _host.Settings.Export.Height,
            _host.Settings.Export.FrameRate);

        DrawHeader(renderer, "Result", meta);
        DrawFrame(renderer);
        DrawTransport(renderer);
    }

    private void DrawFrame(Renderer renderer)
    {
        var frame = FrameBounds;
        renderer.FillRect(frame, Theme.FrameVoid);

        var texture = _host.Player.CurrentTexture;
        if (texture is null || _host.Timeline.Clips.Count == 0)
        {
            renderer.DrawTextCentered(
                _host.Timeline.Clips.Count == 0 ? "Timeline is empty" : "Preparing frame…",
                frame,
                Theme.FontSizeBody,
                Theme.TextFaint);
            return;
        }

        var inner = new FloatRect(
            new Vector2f(frame.Left + 10, frame.Top + 10),
            new Vector2f(Math.Max(1, frame.Width - 20), Math.Max(1, frame.Height - 20)));

        var aspect = _host.Player.SourceAspect <= 0 ? 16.0 / 9.0 : _host.Player.SourceAspect;
        var width = inner.Width;
        var height = (float)(width / aspect);

        if (height > inner.Height)
        {
            height = inner.Height;
            width = (float)(height * aspect);
        }

        var target = new FloatRect(
            new Vector2f(
                MathF.Round(inner.Left + ((inner.Width - width) / 2f)),
                MathF.Round(inner.Top + ((inner.Height - height) / 2f))),
            new Vector2f(MathF.Round(width), MathF.Round(height)));

        renderer.DrawTexture(texture, target);

        if (Math.Abs(_host.Player.Rate - 1.0) > 0.001 && _host.Player.IsPlaying)
        {
            var badge = new FloatRect(
                new Vector2f(frame.Left + 16, frame.Top + 16),
                new Vector2f(42, 18));

            renderer.FillRect(badge, Theme.Shade);
            renderer.DrawTextCentered(
                _host.Player.Rate.ToString("0.#", CultureInfo.InvariantCulture) + "×",
                badge,
                Theme.FontSizeLabel,
                Theme.Accent,
                TextFont.Mono);
        }
    }

    private void DrawTransport(Renderer renderer)
    {
        var transport = TransportBounds;
        renderer.FillRect(transport, Theme.Chrome);
        renderer.HorizontalLine(transport.Left, transport.Top, transport.Width, Theme.Line);

        var hasClips = _host.Timeline.Clips.Count > 0;
        _toStart.Enabled = hasClips;
        _toEnd.Enabled = hasClips;
        _playPause.Enabled = hasClips;
        _playPause.Label = _host.Player.IsPlaying ? "❚❚" : "▶";

        foreach (var button in Buttons)
        {
            button.Draw(renderer);
        }

        var duration = _host.Timeline.TotalDuration;
        if (!_scrub.Dragging)
        {
            _scrub.Value = duration > TimeSpan.Zero
                ? (float)Math.Clamp(_host.Player.Position.TotalSeconds / duration.TotalSeconds, 0, 1)
                : 0f;
        }

        _scrub.Draw(renderer);

        renderer.DrawText(
            TimecodeText(),
            transport.Left + transport.Width - TransportPadding,
            transport.Top + ((transport.Height - 14f) / 2f),
            Theme.FontSizeLabel,
            Theme.TextDim,
            TextFont.Mono,
            TextAlign.Right);
    }

    public override void OnMouseMove(Vector2f point)
    {
        foreach (var button in Buttons)
        {
            button.UpdateHover(point);
        }

        _scrub.OnMouseMove(point);
    }

    public override void OnMouseDown(Vector2f point, Mouse.Button button, bool doubleClick)
    {
        if (button != Mouse.Button.Left)
        {
            return;
        }

        foreach (var candidate in Buttons)
        {
            if (candidate.OnMouseDown(point))
            {
                return;
            }
        }

        if (_host.Timeline.Clips.Count == 0)
        {
            return;
        }

        _wasPlayingBeforeScrub = _host.Player.IsPlaying;
        _scrub.OnMouseDown(point);
    }

    public override void OnMouseUp(Vector2f point, Mouse.Button button)
    {
        if (button != Mouse.Button.Left)
        {
            return;
        }

        if (_scrub.OnMouseUp())
        {
            return;
        }

        foreach (var candidate in Buttons)
        {
            if (candidate.OnMouseUp(point))
            {
                return;
            }
        }
    }

    public override void OnMouseLeave()
    {
        foreach (var button in Buttons)
        {
            button.UpdateHover(new Vector2f(-1, -1));
        }
    }
}
