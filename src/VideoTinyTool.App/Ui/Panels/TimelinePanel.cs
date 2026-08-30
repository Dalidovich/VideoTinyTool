using SFML.Graphics;
using SFML.System;
using SFML.Window;
using VideoTinyTool.Commands;
using VideoTinyTool.Domain;
using VideoTinyTool.Localization;
using VideoTinyTool.Ui.Widgets;

namespace VideoTinyTool.Ui.Panels;

public sealed class TimelinePanel : PanelBase
{
    private const float MinPixelsPerSecond = 0.25f;
    private const float MaxPixelsPerSecond = 4000f;
    private const float EdgeGrabWidth = 7f;
    private const float ClipPadding = 8f;

    private static readonly double[] TickSteps =
    [
        0.02, 0.05, 0.1, 0.2, 0.5, 1, 2, 5, 10, 15, 30, 60, 120, 300, 600, 900, 1800, 3600, 7200, 14400
    ];

    private enum DragMode
    {
        None,
        Playhead,
        TrimIn,
        TrimOut,
        MoveClip
    }

    private readonly IEditorHost _host;
    private readonly Slider _zoom = new() { ShowKnob = true, TrackHeight = 3f };

    private float _pixelsPerSecond = 40f;
    private double _scrollSeconds;
    private DragMode _drag = DragMode.None;
    private Clip? _dragClip;
    private TimeSpan _dragOriginalIn;
    private TimeSpan _dragOriginalOut;
    private int _dragFromIndex = -1;
    private int _dragToIndex = -1;
    private bool _wasPlayingBeforeDrag;

    public TimelinePanel(IEditorHost host)
    {
        _host = host;
        _zoom.Value = ZoomToValue(_pixelsPerSecond);
        _zoom.ValueChanged += value => SetZoom(ValueToZoom(value), LaneBounds.Left + (LaneBounds.Width / 2f));
    }

    private FloatRect BodyArea => new(
        new Vector2f(Bounds.Left, Bounds.Top + Theme.PanelHeaderHeight),
        new Vector2f(Bounds.Width, Bounds.Height - Theme.PanelHeaderHeight - Theme.TimelineFooterHeight));

    private FloatRect RulerBounds => new(
        new Vector2f(BodyArea.Left + Theme.TrackHeaderWidth, BodyArea.Top),
        new Vector2f(BodyArea.Width - Theme.TrackHeaderWidth, Theme.RulerHeight));

    private FloatRect LaneBounds => new(
        new Vector2f(BodyArea.Left + Theme.TrackHeaderWidth, BodyArea.Top + Theme.RulerHeight),
        new Vector2f(BodyArea.Width - Theme.TrackHeaderWidth, BodyArea.Height - Theme.RulerHeight));

    private FloatRect FooterBounds => new(
        new Vector2f(Bounds.Left, Bounds.Top + Bounds.Height - Theme.TimelineFooterHeight),
        new Vector2f(Bounds.Width, Theme.TimelineFooterHeight));

    private float TimeToX(TimeSpan time) =>
        LaneBounds.Left + (float)((time.TotalSeconds - _scrollSeconds) * _pixelsPerSecond);

    private TimeSpan XToTime(float x) =>
        TimeSpan.FromSeconds(_scrollSeconds + ((x - LaneBounds.Left) / _pixelsPerSecond));

    public void Layout(Renderer renderer)
    {
        var footer = FooterBounds;
        _zoom.Bounds = new FloatRect(
            new Vector2f(footer.Left + footer.Width - Theme.Padding - 96f, footer.Top + ((footer.Height - 10f) / 2f)),
            new Vector2f(96f, 10f));
    }

    public override void Draw(Renderer renderer)
    {
        renderer.FillRect(Bounds, Theme.Panel);
        renderer.HorizontalLine(Bounds.Left, Bounds.Top, Bounds.Width, Theme.Line);

        var clipCount = _host.Timeline.Clips.Count;
        DrawHeader(renderer, I18n.Timeline.Title, I18n.Timeline.ClipCount(clipCount));

        ClampScroll();
        DrawRuler(renderer);
        DrawLane(renderer);
        DrawPlayhead(renderer);
        DrawFooter(renderer);
    }

    private void DrawRuler(Renderer renderer)
    {
        var ruler = RulerBounds;

        renderer.FillRect(
            new FloatRect(new Vector2f(BodyArea.Left, ruler.Top), new Vector2f(Theme.TrackHeaderWidth, ruler.Height)),
            Theme.Chrome);
        renderer.FillRect(ruler, Theme.RulerFace);
        renderer.HorizontalLine(BodyArea.Left, ruler.Top + ruler.Height - 1, BodyArea.Width, Theme.Line);

        var step = ChooseTickStep();
        var first = Math.Floor(_scrollSeconds / step) * step;
        var visibleSeconds = ruler.Width / _pixelsPerSecond;

        renderer.PushClip(ruler);

        for (var t = first; t <= _scrollSeconds + visibleSeconds; t += step)
        {
            if (t < 0)
            {
                continue;
            }

            var x = MathF.Round(TimeToX(TimeSpan.FromSeconds(t)));
            renderer.VerticalLine(x, ruler.Top + 5, ruler.Height - 6, Theme.TextFaint);
            renderer.DrawText(
                step < 1 ? TimeFormat.Seconds(TimeSpan.FromSeconds(t)) : TimeFormat.Short(TimeSpan.FromSeconds(t)),
                x + 4,
                ruler.Top + 4,
                Theme.FontSizeLabel,
                Theme.TextFaint,
                TextFont.Mono);
        }

        renderer.PopClip();
    }

    private void DrawLane(Renderer renderer)
    {
        var lane = LaneBounds;

        renderer.FillRect(
            new FloatRect(new Vector2f(BodyArea.Left, lane.Top), new Vector2f(Theme.TrackHeaderWidth, lane.Height)),
            Theme.Chrome);
        renderer.VerticalLine(BodyArea.Left + Theme.TrackHeaderWidth - 1, lane.Top, lane.Height, Theme.Line);
        renderer.DrawTextCentered(
            "V1",
            new FloatRect(new Vector2f(BodyArea.Left, lane.Top), new Vector2f(Theme.TrackHeaderWidth, lane.Height)),
            Theme.FontSizeLabel,
            Theme.TextDim);

        renderer.FillRect(lane, Theme.LaneFace);
        renderer.PushClip(lane);

        var start = TimeSpan.Zero;
        foreach (var clip in _host.Timeline.Clips)
        {
            start += clip.LeadingGap;
            DrawClip(renderer, clip, start);
            start += clip.Duration;
        }

        if (_drag == DragMode.MoveClip && _dragToIndex >= 0)
        {
            DrawInsertionMarker(renderer);
        }

        renderer.PopClip();
    }

    private void DrawClip(Renderer renderer, Clip clip, TimeSpan globalStart)
    {
        var lane = LaneBounds;
        var left = TimeToX(globalStart);
        var right = TimeToX(globalStart + clip.Duration);

        if (right < lane.Left - 4 || left > lane.Left + lane.Width + 4)
        {
            return;
        }

        var bounds = new FloatRect(
            new Vector2f(MathF.Round(left), lane.Top + ClipPadding),
            new Vector2f(Math.Max(2f, MathF.Round(right - left) - 2f), lane.Height - (ClipPadding * 2)));

        var selected = _host.SelectedClip is not null && _host.SelectedClip.Id == clip.Id;
        var missing = _host.SourceMissing(clip.SourceId);
        var source = _host.FindSource(clip.SourceId);

        renderer.FillRect(bounds, missing ? Theme.ClipMissing : selected ? Theme.ClipSelected : Theme.ClipFace);

        if (!missing && source is not null && bounds.Width > 8)
        {
            DrawFilmstrip(renderer, clip, source, bounds);
        }

        renderer.StrokeRect(bounds, missing ? Theme.ClipMissingBorder : selected ? Theme.Accent : Theme.ClipBorder);

        renderer.PushClip(bounds);

        renderer.FillRect(
            new FloatRect(bounds.Position, new Vector2f(bounds.Width, 17)),
            Theme.WithAlpha(Theme.FrameVoid, 190));

        var name = source?.FileName ?? I18n.Timeline.MissingSource;
        renderer.DrawText(
            renderer.Ellipsize(name, bounds.Width - 10, Theme.FontSizeLabel),
            bounds.Left + 5,
            bounds.Top + 2,
            Theme.FontSizeLabel,
            missing ? Theme.ClipMissingBorder : Theme.Text);

        var duration = TimeFormat.Seconds(clip.Duration);
        var durationWidth = renderer.MeasureText(duration, Theme.FontSizeLabel, TextFont.Mono);
        if (durationWidth + 12 < bounds.Width)
        {
            var badge = new FloatRect(
                new Vector2f(bounds.Left + bounds.Width - durationWidth - 9, bounds.Top + bounds.Height - 17),
                new Vector2f(durationWidth + 6, 14));

            renderer.FillRect(badge, Theme.WithAlpha(Theme.FrameVoid, 170));
            renderer.DrawText(
                duration,
                badge.Left + 3,
                badge.Top + 1,
                Theme.FontSizeLabel,
                Theme.Text,
                TextFont.Mono);
        }

        renderer.PopClip();

        if (selected && bounds.Width > EdgeGrabWidth * 2)
        {
            renderer.FillRect(bounds.Left, bounds.Top, EdgeGrabWidth, bounds.Height, Theme.Accent);
            renderer.FillRect(
                bounds.Left + bounds.Width - EdgeGrabWidth,
                bounds.Top,
                EdgeGrabWidth,
                bounds.Height,
                Theme.Accent);
        }
    }

    private void DrawFilmstrip(Renderer renderer, Clip clip, Domain.MediaSource source, FloatRect bounds)
    {
        renderer.PushClip(bounds);

        var frameHeight = bounds.Height;
        var frameWidth = MathF.Max(24f, (float)(frameHeight * source.AspectRatio));
        var lane = LaneBounds;
        var firstIndex = (int)Math.Floor(Math.Max(0, lane.Left - bounds.Left) / frameWidth);

        for (var i = firstIndex; i * frameWidth < bounds.Width; i++)
        {
            var x = bounds.Left + (i * frameWidth);
            if (x > lane.Left + lane.Width)
            {
                break;
            }

            var offsetSeconds = (i * frameWidth) / _pixelsPerSecond;
            var sourceTime = clip.In + TimeSpan.FromSeconds(offsetSeconds);
            if (sourceTime >= clip.Out)
            {
                break;
            }

            var texture = _host.Thumbnails.Get(source, sourceTime);
            var cell = new FloatRect(new Vector2f(x, bounds.Top), new Vector2f(frameWidth, frameHeight));

            if (texture is not null)
            {
                renderer.DrawTextureCover(texture, cell);
            }

            renderer.VerticalLine(x, bounds.Top, frameHeight, Theme.WithAlpha(Theme.FrameVoid, 70));
        }

        renderer.PopClip();
    }

    private void DrawInsertionMarker(Renderer renderer)
    {
        var lane = LaneBounds;
        var x = MathF.Round(TimeToX(_host.Timeline.StartOf(_dragToIndex)));
        renderer.FillRect(x - 1, lane.Top + 4, 2, lane.Height - 8, Theme.Accent);
    }

    private void DrawPlayhead(Renderer renderer)
    {
        var body = BodyArea;
        var x = MathF.Round(TimeToX(_host.Player.Position));

        if (x < body.Left + Theme.TrackHeaderWidth || x > body.Left + body.Width)
        {
            return;
        }

        renderer.PushClip(new FloatRect(
            new Vector2f(body.Left + Theme.TrackHeaderWidth, body.Top),
            new Vector2f(body.Width - Theme.TrackHeaderWidth, body.Height)));

        renderer.FillRect(x, body.Top, 1, body.Height, Theme.Accent);
        renderer.DrawTriangle(
            new Vector2f(x - 5, body.Top),
            new Vector2f(x + 6, body.Top),
            new Vector2f(x + 0.5f, body.Top + 7),
            Theme.Accent);

        renderer.PopClip();
    }

    private void DrawFooter(Renderer renderer)
    {
        var footer = FooterBounds;
        renderer.FillRect(footer, Theme.Chrome);
        renderer.HorizontalLine(footer.Left, footer.Top, footer.Width, Theme.Line);

        var selected = _host.SelectedClip;
        var inText = I18n.Timeline.InPrefix
                     + (selected is null ? I18n.Timeline.NoTimecode : TimeFormat.Timecode(selected.In));
        var outText = I18n.Timeline.OutPrefix
                      + (selected is null ? I18n.Timeline.NoTimecode : TimeFormat.Timecode(selected.Out));

        var y = footer.Top + 5;
        renderer.DrawText(inText, footer.Left + Theme.Padding, y, Theme.FontSizeLabel, Theme.TextFaint, TextFont.Mono);
        renderer.DrawText(outText, footer.Left + Theme.Padding + 130, y, Theme.FontSizeLabel, Theme.TextFaint, TextFont.Mono);

        renderer.DrawText(
            I18n.Timeline.Zoom,
            _zoom.Bounds.Left - 8,
            y,
            Theme.FontSizeLabel,
            Theme.TextFaint,
            TextFont.Regular,
            TextAlign.Right);

        _zoom.FillColor = Theme.LineStrong;
        _zoom.Draw(renderer);
    }

    public void ZoomBy(float factor) => SetZoom(_pixelsPerSecond * factor, LaneBounds.Left + (LaneBounds.Width / 2f));

    public void ScrollToPlayhead()
    {
        var lane = LaneBounds;
        if (lane.Width <= 0)
        {
            return;
        }

        var position = _host.Player.Position.TotalSeconds;
        var visible = lane.Width / _pixelsPerSecond;

        if (position < _scrollSeconds)
        {
            _scrollSeconds = Math.Max(0, position - (visible * 0.25));
        }
        else if (position > _scrollSeconds + (visible * 0.92))
        {
            _scrollSeconds = Math.Max(0, position - (visible * 0.5));
        }

        ClampScroll();
    }

    private void SetZoom(float pixelsPerSecond, float anchorX)
    {
        var anchorTime = XToTime(anchorX).TotalSeconds;
        _pixelsPerSecond = Math.Clamp(pixelsPerSecond, MinPixelsPerSecond, MaxPixelsPerSecond);
        _scrollSeconds = anchorTime - ((anchorX - LaneBounds.Left) / _pixelsPerSecond);
        _zoom.Value = ZoomToValue(_pixelsPerSecond);
        ClampScroll();
    }

    private static float ZoomToValue(float pixelsPerSecond) =>
        (float)((Math.Log(pixelsPerSecond) - Math.Log(MinPixelsPerSecond))
                / (Math.Log(MaxPixelsPerSecond) - Math.Log(MinPixelsPerSecond)));

    private static float ValueToZoom(float value) =>
        (float)Math.Exp(Math.Log(MinPixelsPerSecond)
                        + (value * (Math.Log(MaxPixelsPerSecond) - Math.Log(MinPixelsPerSecond))));

    private void ClampScroll()
    {
        var lane = LaneBounds;
        var visible = lane.Width / _pixelsPerSecond;
        var total = _host.Timeline.TotalDuration.TotalSeconds;
        var max = Math.Max(0, total - (visible * 0.5));
        _scrollSeconds = Math.Clamp(_scrollSeconds, 0, max);
    }

    private double ChooseTickStep()
    {
        foreach (var step in TickSteps)
        {
            if (step * _pixelsPerSecond >= 74)
            {
                return step;
            }
        }

        return TickSteps[^1];
    }

    private (Clip Clip, int Index, FloatRect Bounds)? ClipAt(Vector2f point)
    {
        var lane = LaneBounds;
        if (point.Y < lane.Top || point.Y > lane.Top + lane.Height)
        {
            return null;
        }

        var start = TimeSpan.Zero;
        for (var i = 0; i < _host.Timeline.Clips.Count; i++)
        {
            var clip = _host.Timeline.Clips[i];
            start += clip.LeadingGap;

            var left = TimeToX(start);
            var right = TimeToX(start + clip.Duration);

            if (point.X >= left && point.X < right)
            {
                return (clip, i, new FloatRect(
                    new Vector2f(left, lane.Top + ClipPadding),
                    new Vector2f(right - left, lane.Height - (ClipPadding * 2))));
            }

            start += clip.Duration;
        }

        return null;
    }

    public override void OnMouseDown(Vector2f point, Mouse.Button button, bool doubleClick)
    {
        if (button != Mouse.Button.Left)
        {
            return;
        }

        if (_zoom.OnMouseDown(point))
        {
            return;
        }

        var body = BodyArea;
        if (point.Y < body.Top || point.Y > body.Top + body.Height || point.X < body.Left + Theme.TrackHeaderWidth)
        {
            return;
        }

        if (point.Y < RulerBounds.Top + RulerBounds.Height)
        {
            BeginPlayheadDrag(point);
            return;
        }

        var hit = ClipAt(point);
        if (hit is null)
        {
            _host.SelectClip(null);
            BeginPlayheadDrag(point);
            return;
        }

        var (clip, index, bounds) = hit.Value;
        var alreadySelected = _host.SelectedClip is not null && _host.SelectedClip.Id == clip.Id;
        _host.SelectClip(clip);

        if (alreadySelected && bounds.Width > EdgeGrabWidth * 2)
        {
            if (point.X <= bounds.Left + EdgeGrabWidth)
            {
                StartTrim(clip, DragMode.TrimIn);
                return;
            }

            if (point.X >= bounds.Left + bounds.Width - EdgeGrabWidth)
            {
                StartTrim(clip, DragMode.TrimOut);
                return;
            }
        }

        _drag = DragMode.MoveClip;
        _dragClip = clip;
        _dragFromIndex = index;
        _dragToIndex = index;
    }

    private void BeginPlayheadDrag(Vector2f point)
    {
        _drag = DragMode.Playhead;
        _wasPlayingBeforeDrag = _host.Player.IsPlaying;
        _host.SeekTo(ClampToTimeline(XToTime(point.X)), true);
    }

    private void StartTrim(Clip clip, DragMode mode)
    {
        _drag = mode;
        _dragClip = clip;
        _dragOriginalIn = clip.In;
        _dragOriginalOut = clip.Out;
        _wasPlayingBeforeDrag = _host.Player.IsPlaying;
        _host.Player.Pause();
    }

    public override void OnMouseMove(Vector2f point)
    {
        if (_zoom.OnMouseMove(point))
        {
            return;
        }

        switch (_drag)
        {
            case DragMode.Playhead:
                _host.SeekTo(ClampToTimeline(XToTime(point.X)), true);
                break;

            case DragMode.TrimIn:
            case DragMode.TrimOut:
                ApplyTrim(point);
                break;

            case DragMode.MoveClip:
                _dragToIndex = TargetIndexFor(point);
                break;
        }
    }

    private void ApplyTrim(Vector2f point)
    {
        if (_dragClip is null)
        {
            return;
        }

        var source = _host.FindSource(_dragClip.SourceId);
        var minimum = EditRules.MinimumDuration(source);
        var clipStart = _host.Timeline.StartOf(_dragClip);
        var pointer = XToTime(point.X);

        if (_drag == DragMode.TrimIn)
        {
            var desired = _dragOriginalIn + (pointer - clipStart);
            if (EditRules.TryTrimIn(_dragClip, desired, minimum, out var newIn))
            {
                _host.Timeline.SetBounds(_dragClip, newIn, _dragClip.Out);
            }

            return;
        }

        var desiredOut = _dragClip.Out + (pointer - (clipStart + _dragClip.Duration));
        var sourceDuration = source?.Duration ?? _dragClip.Out;

        if (EditRules.TryTrimOut(_dragClip, desiredOut, minimum, sourceDuration, out var newOut))
        {
            _host.Timeline.SetBounds(_dragClip, _dragClip.In, newOut);
        }
    }

    private int TargetIndexFor(Vector2f point)
    {
        var clips = _host.Timeline.Clips;
        var start = TimeSpan.Zero;

        for (var i = 0; i < clips.Count; i++)
        {
            start += clips[i].LeadingGap;

            var left = TimeToX(start);
            var right = TimeToX(start + clips[i].Duration);

            if (point.X < (left + right) / 2f)
            {
                return i;
            }

            start += clips[i].Duration;
        }

        return clips.Count - 1;
    }

    public override void OnMouseUp(Vector2f point, Mouse.Button button)
    {
        if (button != Mouse.Button.Left)
        {
            return;
        }

        if (_zoom.OnMouseUp())
        {
            return;
        }

        switch (_drag)
        {
            case DragMode.Playhead:
                _host.EndScrub(_wasPlayingBeforeDrag);
                break;

            case DragMode.TrimIn:
            case DragMode.TrimOut:
                CommitTrim();
                break;

            case DragMode.MoveClip:
                CommitReorder();
                break;
        }

        _drag = DragMode.None;
        _dragClip = null;
        _dragFromIndex = -1;
        _dragToIndex = -1;
    }

    private void CommitTrim()
    {
        if (_dragClip is null)
        {
            return;
        }

        var newIn = _dragClip.In;
        var newOut = _dragClip.Out;

        if (newIn == _dragOriginalIn && newOut == _dragOriginalOut)
        {
            return;
        }

        _host.Timeline.SetBounds(_dragClip, _dragOriginalIn, _dragOriginalOut);
        _host.Execute(new TrimClipCommand(_dragClip, newIn, newOut));
    }

    private void CommitReorder()
    {
        if (_dragClip is null || _dragFromIndex < 0 || _dragToIndex < 0 || _dragFromIndex == _dragToIndex)
        {
            return;
        }

        _host.Execute(new ReorderClipCommand(_dragClip, _dragFromIndex, _dragToIndex));
    }

    public override void OnScroll(Vector2f point, float delta, bool control)
    {
        if (control)
        {
            SetZoom(_pixelsPerSecond * (delta > 0 ? 1.18f : 1f / 1.18f), point.X);
            return;
        }

        _scrollSeconds -= delta * (60f / _pixelsPerSecond);
        ClampScroll();
    }

    private TimeSpan ClampToTimeline(TimeSpan value) => Clamp(value, TimeSpan.Zero, _host.Timeline.TotalDuration);

    private static TimeSpan Clamp(TimeSpan value, TimeSpan min, TimeSpan max)
    {
        if (max < min)
        {
            return min;
        }

        return value < min ? min : value > max ? max : value;
    }
}
