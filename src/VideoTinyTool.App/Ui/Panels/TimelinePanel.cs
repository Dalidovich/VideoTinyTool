using SFML.Graphics;
using SFML.System;
using SFML.Window;
using VideoTinyTool.Commands;
using VideoTinyTool.Domain;
using VideoTinyTool.Localization;
using VideoTinyTool.Media;
using VideoTinyTool.Ui.Widgets;

namespace VideoTinyTool.Ui.Panels;

public sealed class TimelinePanel : PanelBase
{
    private const float MinPixelsPerSecond = 0.25f;
    private const float MaxPixelsPerSecond = 4000f;
    private const float EdgeGrabWidth = 7f;
    private const float ClipPadding = 8f;
    private const float TrackButtonSize = 15f;
    private const float LaneWheelStep = 24f;
    private const float TitleStripHeight = 17f;
    private const int MaxTracks = Domain.Timeline.MaxVideoTracks + Domain.Timeline.MaxAudioTracks;

    private static readonly double[] TickSteps =
    [
        0.02, 0.05, 0.1, 0.2, 0.5, 1, 2, 5, 10, 15, 30, 60, 120, 300, 600, 900, 1800, 3600, 7200, 14400
    ];

    private enum ClipLook
    {
        Base,
        Overlay,
        Audio
    }

    private enum DragMode
    {
        None,
        Playhead,
        TrimIn,
        TrimOut,
        MoveClip
    }

    private readonly record struct ClipHit(Clip Clip, int TrackIndex, int Index, FloatRect Bounds);

    private readonly IEditorHost _host;
    private readonly Slider _zoom = new() { ShowKnob = true, TrackHeight = 3f };
    private readonly Button _addTrack = new(I18n.Timeline.AddTrack);
    private readonly Button _addAudioTrack = new(I18n.Timeline.AddAudioTrack);
    private readonly List<Button> _removeTrack = new();

    private float _pixelsPerSecond = 40f;
    private double _scrollSeconds;
    private float _laneScroll;
    private DragMode _drag = DragMode.None;
    private Clip? _dragClip;
    private TimeSpan _dragOriginalIn;
    private TimeSpan _dragOriginalOut;
    private TimeSpan _dragGrabOffset;
    private TimeSpan _dragStart;
    private int _dragFromTrack = -1;
    private int _dragToTrack = -1;
    private int _dragFromIndex = -1;
    private int _dragToIndex = -1;
    private bool _dragRejected;
    private bool _wasPlayingBeforeDrag;

    public TimelinePanel(IEditorHost host)
    {
        _host = host;
        _zoom.Value = ZoomToValue(_pixelsPerSecond);
        _zoom.ValueChanged += value => SetZoom(ValueToZoom(value), LaneBounds.Left + (LaneBounds.Width / 2f));

        _addTrack.Clicked += _host.AddTrack;
        _addAudioTrack.Clicked += _host.AddAudioTrack;

        for (var track = 1; track < MaxTracks; track++)
        {
            var index = track;
            var button = new Button(I18n.Timeline.RemoveTrack, ButtonStyle.Ghost);
            button.Clicked += () => _host.RemoveTrack(index);
            _removeTrack.Add(button);
        }
    }

    public override void RefreshText()
    {
        _addTrack.Label = I18n.Timeline.AddTrack;
        _addAudioTrack.Label = I18n.Timeline.AddAudioTrack;

        foreach (var button in _removeTrack)
        {
            button.Label = I18n.Timeline.RemoveTrack;
        }
    }

    private IEnumerable<Button> TrackButtons
    {
        get
        {
            yield return _addTrack;
            yield return _addAudioTrack;
            foreach (var button in _removeTrack)
            {
                yield return button;
            }
        }
    }

    private int TrackCount => _host.Timeline.Tracks.Count;

    private int VideoTrackCount => _host.Timeline.VideoTrackCount;

    private FloatRect BodyArea => new(
        new Vector2f(Bounds.Left, Bounds.Top + Theme.PanelHeaderHeight),
        new Vector2f(Bounds.Width, Bounds.Height - Theme.PanelHeaderHeight - Theme.TimelineFooterHeight));

    private FloatRect RulerBounds => new(
        new Vector2f(BodyArea.Left + Theme.TrackHeaderWidth, BodyArea.Top),
        new Vector2f(BodyArea.Width - Theme.TrackHeaderWidth, Theme.RulerHeight));

    private FloatRect LaneBounds => new(
        new Vector2f(BodyArea.Left + Theme.TrackHeaderWidth, BodyArea.Top + Theme.RulerHeight),
        new Vector2f(BodyArea.Width - Theme.TrackHeaderWidth, BodyArea.Height - Theme.RulerHeight));

    private FloatRect HeaderColumn => new(
        new Vector2f(BodyArea.Left, LaneBounds.Top),
        new Vector2f(Theme.TrackHeaderWidth, LaneBounds.Height));

    private FloatRect FooterBounds => new(
        new Vector2f(Bounds.Left, Bounds.Top + Bounds.Height - Theme.TimelineFooterHeight),
        new Vector2f(Bounds.Width, Theme.TimelineFooterHeight));

    private float LaneHeight => LaneGeometry.LaneHeight(LaneBounds.Height, TrackCount);

    private bool IsReorderDrag => _dragFromTrack == 0 && _dragToTrack == 0;

    private float TimeToX(TimeSpan time) =>
        LaneBounds.Left + (float)((time.TotalSeconds - _scrollSeconds) * _pixelsPerSecond);

    private TimeSpan XToTime(float x) =>
        TimeSpan.FromSeconds(_scrollSeconds + ((x - LaneBounds.Left) / _pixelsPerSecond));

    private static bool SnapHeld =>
        Keyboard.IsKeyPressed(Keyboard.Key.LControl) || Keyboard.IsKeyPressed(Keyboard.Key.RControl);

    private List<TimeSpan> SnapPoints(Clip? exclude, bool includePlayhead)
    {
        var points = new List<TimeSpan> { TimeSpan.Zero };

        for (var track = 0; track < TrackCount; track++)
        {
            var start = TimeSpan.Zero;

            foreach (var clip in _host.Timeline.ClipsOf(track))
            {
                start += clip.LeadingGap;

                if (!ReferenceEquals(clip, exclude))
                {
                    points.Add(start);
                    points.Add(start + clip.Duration);
                }

                start += clip.Duration;
            }
        }

        if (includePlayhead)
        {
            points.Add(_host.Player.Position);
        }

        return points;
    }

    private TimeSpan PointerTime(float x, Clip? exclude, bool includePlayhead)
    {
        var time = XToTime(x);
        return SnapHeld ? SnapGeometry.Snap(time, SnapPoints(exclude, includePlayhead), _pixelsPerSecond) : time;
    }

    private FloatRect LaneRect(int trackIndex)
    {
        var lane = LaneBounds;
        return new FloatRect(
            new Vector2f(
                lane.Left,
                lane.Top + LaneGeometry.LaneOffset(
                    lane.Height,
                    VideoTrackCount,
                    TrackCount,
                    trackIndex,
                    _laneScroll)),
            new Vector2f(lane.Width, LaneHeight));
    }

    private FloatRect HeaderCell(int trackIndex)
    {
        var row = LaneRect(trackIndex);
        return new FloatRect(
            new Vector2f(BodyArea.Left, row.Top),
            new Vector2f(Theme.TrackHeaderWidth, row.Height));
    }

    private static float ClipInset(float laneHeight) => MathF.Min(ClipPadding, MathF.Round(laneHeight * 0.12f));

    public void Layout(Renderer renderer)
    {
        var footer = FooterBounds;
        _zoom.Bounds = new FloatRect(
            new Vector2f(footer.Left + footer.Width - Theme.Padding - 96f, footer.Top + ((footer.Height - 10f) / 2f)),
            new Vector2f(96f, 10f));

        LayoutTrackButtons();
    }

    private void LayoutTrackButtons()
    {
        _laneScroll = LaneGeometry.ClampScroll(_laneScroll, LaneBounds.Height, TrackCount);

        for (var track = 1; track < MaxTracks; track++)
        {
            var button = _removeTrack[track - 1];
            var cell = HeaderCell(track);

            button.Bounds = track < TrackCount
                ? Reachable(new FloatRect(
                    new Vector2f(cell.Left + cell.Width - TrackButtonSize - 5f, cell.Top + 5f),
                    new Vector2f(TrackButtonSize, TrackButtonSize)))
                : default;
        }

        var baseCell = HeaderCell(0);
        var buttonWidth = (baseCell.Width - 16f) / 2f;
        var buttonTop = baseCell.Top + baseCell.Height - TrackButtonSize - 6f;

        _addTrack.Enabled = VideoTrackCount < Domain.Timeline.MaxVideoTracks;
        _addTrack.Bounds = Reachable(new FloatRect(
            new Vector2f(baseCell.Left + 7f, buttonTop),
            new Vector2f(buttonWidth, TrackButtonSize)));

        _addAudioTrack.Enabled = _host.Timeline.AudioTrackCount < Domain.Timeline.MaxAudioTracks;
        _addAudioTrack.Bounds = Reachable(new FloatRect(
            new Vector2f(baseCell.Left + 9f + buttonWidth, buttonTop),
            new Vector2f(buttonWidth, TrackButtonSize)));
    }

    private FloatRect Reachable(FloatRect bounds)
    {
        var lane = LaneBounds;
        return bounds.Top >= lane.Top && bounds.Top + bounds.Height <= lane.Top + lane.Height ? bounds : default;
    }

    public override void Draw(Renderer renderer)
    {
        renderer.FillRect(Bounds, Theme.Panel);
        renderer.HorizontalLine(Bounds.Left, Bounds.Top, Bounds.Width, Theme.Line);

        var clipCount = _host.Timeline.Tracks.Sum(track => track.Clips.Count);
        DrawHeader(renderer, I18n.Timeline.Title, I18n.Timeline.ClipCount(clipCount));

        ClampScroll();
        DrawRuler(renderer);
        DrawLanes(renderer);
        DrawTrackHeaders(renderer);
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

    private void DrawLanes(Renderer renderer)
    {
        var lane = LaneBounds;

        renderer.FillRect(lane, Theme.LaneFace);
        renderer.PushClip(lane);

        for (var track = TrackCount - 1; track >= 0; track--)
        {
            var row = LaneRect(track);
            if (row.Top + row.Height < lane.Top || row.Top > lane.Top + lane.Height)
            {
                continue;
            }

            if (row.Top > lane.Top)
            {
                renderer.HorizontalLine(row.Left, MathF.Round(row.Top), row.Width, Theme.Line);
            }

            var start = TimeSpan.Zero;
            foreach (var clip in _host.Timeline.ClipsOf(track))
            {
                start += clip.LeadingGap;
                DrawClip(renderer, clip, start, row, LookOf(track));
                start += clip.Duration;
            }
        }

        DrawDropPreview(renderer);
        renderer.PopClip();
    }

    private ClipLook LookOf(int trackIndex) => _host.Timeline.IsAudioTrack(trackIndex)
        ? ClipLook.Audio
        : trackIndex > 0
            ? ClipLook.Overlay
            : ClipLook.Base;

    private TrackKind KindOf(int trackIndex) => trackIndex >= 0 && trackIndex < TrackCount
        ? _host.Timeline.Tracks[trackIndex].Kind
        : TrackKind.Video;

    private void DrawTrackHeaders(Renderer renderer)
    {
        var column = HeaderColumn;

        renderer.FillRect(column, Theme.Chrome);
        renderer.PushClip(column);

        var videoCount = VideoTrackCount;

        for (var track = 0; track < TrackCount; track++)
        {
            var cell = HeaderCell(track);
            if (cell.Top + cell.Height < column.Top || cell.Top > column.Top + column.Height)
            {
                continue;
            }

            if (cell.Top > column.Top)
            {
                renderer.HorizontalLine(cell.Left, MathF.Round(cell.Top), cell.Width, Theme.Line);
            }

            renderer.DrawText(
                track < videoCount
                    ? I18n.Timeline.TrackLabel(track + 1)
                    : I18n.Timeline.AudioTrackLabel(track - videoCount + 1),
                cell.Left + 15f,
                cell.Top + 6f,
                Theme.FontSizeLabel,
                _host.SelectedClip is not null && track == _host.SelectedTrackIndex ? Theme.Text : Theme.TextDim,
                TextFont.SemiBold,
                TextAlign.Center);
        }

        foreach (var button in TrackButtons)
        {
            if (button.Bounds.Width > 0)
            {
                button.Draw(renderer);
            }
        }

        renderer.PopClip();
        renderer.VerticalLine(column.Left + column.Width - 1, column.Top, column.Height, Theme.Line);
    }

    private void DrawClip(Renderer renderer, Clip clip, TimeSpan globalStart, FloatRect row, ClipLook look)
    {
        var lane = LaneBounds;
        var left = TimeToX(globalStart);
        var right = TimeToX(globalStart + clip.Duration);

        if (right < lane.Left - 4 || left > lane.Left + lane.Width + 4)
        {
            return;
        }

        var inset = ClipInset(row.Height);
        var bounds = new FloatRect(
            new Vector2f(MathF.Round(left), row.Top + inset),
            new Vector2f(Math.Max(2f, MathF.Round(right - left) - 2f), row.Height - (inset * 2)));

        var selected = _host.SelectedClip is not null && _host.SelectedClip.Id == clip.Id;
        var missing = _host.SourceMissing(clip.SourceId);
        var source = _host.FindSource(clip.SourceId);

        renderer.FillRect(bounds, missing
            ? Theme.ClipMissing
            : selected
                ? Theme.ClipSelected
                : look switch
                {
                    ClipLook.Overlay => Theme.ClipOverlayFace,
                    ClipLook.Audio => Theme.ClipAudioFace,
                    _ => Theme.ClipFace
                });

        if (!missing && look != ClipLook.Audio && source is { HasVideo: true } && bounds.Width > 8)
        {
            DrawFilmstrip(renderer, clip, source, bounds);
        }

        if (!missing && look == ClipLook.Audio && source is not null && bounds.Width > 2)
        {
            DrawWaveform(renderer, clip, source, bounds, selected);
        }

        renderer.StrokeRect(bounds, missing
            ? Theme.ClipMissingBorder
            : selected
                ? Theme.Accent
                : look switch
                {
                    ClipLook.Overlay => Theme.ClipOverlayBorder,
                    ClipLook.Audio => Theme.ClipAudioBorder,
                    _ => Theme.ClipBorder
                });

        renderer.PushClip(bounds);

        renderer.FillRect(
            new FloatRect(bounds.Position, new Vector2f(bounds.Width, 17)),
            Theme.WithAlpha(Theme.FrameVoid, 190));

        var badges = DrawAudioBadges(renderer, clip, bounds);
        var name = source?.FileName ?? I18n.Timeline.MissingSource;
        renderer.DrawText(
            renderer.Ellipsize(name, Math.Max(0f, bounds.Width - 10 - badges), Theme.FontSizeLabel),
            bounds.Left + 5,
            bounds.Top + 2,
            Theme.FontSizeLabel,
            missing ? Theme.ClipMissingBorder : Theme.Text);

        var duration = TimeFormat.Seconds(clip.Duration);
        var durationWidth = renderer.MeasureText(duration, Theme.FontSizeLabel, TextFont.Mono);
        if (durationWidth + 12 < bounds.Width && bounds.Height > 36)
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

    private static float DrawAudioBadges(Renderer renderer, Clip clip, FloatRect bounds)
    {
        var right = bounds.Left + bounds.Width - 3f;
        var consumed = 0f;

        if (clip.Audio.Muted)
        {
            var width = DrawBadge(renderer, I18n.Timeline.MutedBadge, right, bounds.Top + 2f, Theme.ClipMissingBorder);
            right -= width + 3f;
            consumed += width + 3f;
        }

        if (MathF.Abs(clip.Audio.Volume - 1f) > 0.005f)
        {
            var text = I18n.Timeline.VolumeBadge(clip.Audio.Volume);
            consumed += DrawBadge(renderer, text, right, bounds.Top + 2f, Theme.Accent) + 3f;
        }

        return consumed;
    }

    private static float DrawBadge(Renderer renderer, string text, float right, float top, Color ink)
    {
        var width = MathF.Round(renderer.MeasureText(text, Theme.FontSizeLabel, TextFont.Mono)) + 6f;
        var badge = new FloatRect(new Vector2f(right - width, top), new Vector2f(width, 13f));

        renderer.FillRect(badge, Theme.WithAlpha(Theme.FrameVoid, 200));
        renderer.DrawText(text, badge.Left + 3f, badge.Top, Theme.FontSizeLabel, ink, TextFont.Mono);

        return width;
    }

    private void DrawWaveform(
        Renderer renderer,
        Clip clip,
        Domain.MediaSource source,
        FloatRect bounds,
        bool selected)
    {
        var top = bounds.Height > TitleStripHeight + 8f ? bounds.Top + TitleStripHeight : bounds.Top;
        var height = bounds.Top + bounds.Height - top;
        var centre = MathF.Round(top + (height / 2f));
        var half = MathF.Max(1f, (height / 2f) - 1f);
        var ink = selected ? Theme.Waveform : Theme.WaveformDim;

        renderer.PushClip(bounds);
        renderer.HorizontalLine(bounds.Left, centre, bounds.Width, ink);

        var peaks = source.HasAudio ? _host.Waveforms.Get(source) : null;
        var gain = clip.Audio.Gain;

        if (peaks is { Length: > 0 } && gain > 0f)
        {
            var lane = LaneBounds;
            var first = (int)MathF.Max(0f, lane.Left - bounds.Left);
            var last = (int)MathF.Min(bounds.Width, lane.Left + lane.Width - bounds.Left);
            var span = peaks.AsSpan();

            for (var column = first; column < last; column++)
            {
                var (from, to) = WaveformGeometry.ColumnRange(clip.In, column, 1f, _pixelsPerSecond);
                if (from >= clip.Out)
                {
                    break;
                }

                var peak = WaveformGeometry.PeakBetween(
                    span,
                    WaveformGeometry.BucketAt(from, WaveformService.BucketsPerSecond),
                    WaveformGeometry.BucketAt(to, WaveformService.BucketsPerSecond));

                var amplitude = peak / 255f * gain * half;
                if (amplitude < 0.5f)
                {
                    continue;
                }

                renderer.FillRect(bounds.Left + column, centre - amplitude, 1f, amplitude * 2f, ink);
            }
        }

        renderer.PopClip();
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

    private void DrawDropPreview(Renderer renderer)
    {
        if (_drag != DragMode.MoveClip || _dragClip is null || _dragToTrack < 0)
        {
            return;
        }

        var row = LaneRect(_dragToTrack);
        var inset = ClipInset(row.Height);

        if (IsReorderDrag)
        {
            if (_dragToIndex < 0)
            {
                return;
            }

            var marker = MathF.Round(TimeToX(_host.Timeline.StartOf(_dragToIndex)));
            renderer.FillRect(marker - 1, row.Top + 4, 2, row.Height - 8, Theme.Accent);
            return;
        }

        var left = TimeToX(_dragStart);
        var right = TimeToX(_dragStart + _dragClip.Duration);
        var ghost = new FloatRect(
            new Vector2f(MathF.Round(left), row.Top + inset),
            new Vector2f(Math.Max(2f, MathF.Round(right - left)), row.Height - (inset * 2)));

        var ink = _dragRejected ? Theme.ClipMissingBorder : Theme.Accent;

        if (_dragToTrack > 0 || _dragRejected)
        {
            renderer.FillRect(ghost, _dragRejected ? Theme.WithAlpha(Theme.ClipMissing, 110) : Theme.AccentSoft);
            renderer.StrokeRect(ghost, ink);
        }

        renderer.FillRect(ghost.Left - 1, row.Top + 4, 2, row.Height - 8, ink);
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
        _laneScroll = LaneGeometry.ClampScroll(_laneScroll, lane.Height, TrackCount);
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

    private int TrackAt(Vector2f point) =>
        LaneGeometry.TrackIndexAt(
            LaneBounds.Height,
            VideoTrackCount,
            TrackCount,
            _laneScroll,
            point.Y - LaneBounds.Top);

    private ClipHit? ClipAt(Vector2f point)
    {
        var lane = LaneBounds;
        var track = TrackAt(point);
        if (track < 0 || point.X < lane.Left)
        {
            return null;
        }

        var row = LaneRect(track);
        var inset = ClipInset(row.Height);
        var clips = _host.Timeline.ClipsOf(track);
        var start = TimeSpan.Zero;

        for (var i = 0; i < clips.Count; i++)
        {
            var clip = clips[i];
            start += clip.LeadingGap;

            var left = TimeToX(start);
            var right = TimeToX(start + clip.Duration);

            if (point.X >= left && point.X < right)
            {
                return new ClipHit(clip, track, i, new FloatRect(
                    new Vector2f(left, row.Top + inset),
                    new Vector2f(right - left, row.Height - (inset * 2))));
            }

            start += clip.Duration;
        }

        return null;
    }

    private void ShowMenu(Vector2f point)
    {
        if (_drag != DragMode.None)
        {
            return;
        }

        var body = BodyArea;
        if (point.Y < body.Top || point.Y > body.Top + body.Height)
        {
            return;
        }

        var menu = new ContextMenu(point);
        var overHeader = point.X < body.Left + Theme.TrackHeaderWidth;
        var overRuler = point.Y < RulerBounds.Top + RulerBounds.Height;

        if (!overHeader && !overRuler && ClipAt(point) is { } hit)
        {
            _host.SelectClip(hit.Clip);
            AddClipItems(menu, hit.Clip, hit.TrackIndex);
            menu.Separator();
            AddHistoryItems(menu);
        }
        else
        {
            AddTrackItems(menu, overRuler ? -1 : TrackAt(point));
            menu.Separator();
            AddHistoryItems(menu);
            menu.Separator();
            menu.Add(I18n.Menu.Import, "Ctrl+O", true, _host.ImportFiles);
            menu.Add(I18n.Menu.Export, "Ctrl+M", _host.Timeline.HasClips, _host.ExportTimeline);
        }

        _host.ShowContextMenu(menu);
    }

    private void AddClipItems(ContextMenu menu, Clip clip, int trackIndex)
    {
        var source = _host.FindSource(clip.SourceId);
        var offset = _host.Player.Position - _host.Timeline.StartOf(clip);
        var inside = offset > TimeSpan.Zero && offset < clip.Duration;

        menu.Add(
            I18n.Menu.Split,
            "S",
            EditRules.CanSplit(clip, offset, EditRules.MinimumDuration(source)),
            _host.SplitAtPlayhead);

        menu.Add(I18n.Menu.TrimIn, "I", inside, () => _host.TrimSelectedToPlayhead(true));
        menu.Add(I18n.Menu.TrimOut, "O", inside, () => _host.TrimSelectedToPlayhead(false));
        menu.Separator();

        menu.Add(
            I18n.Menu.DetachAudio,
            "Ctrl+U",
            source is { HasAudio: true } && !_host.Timeline.IsAudioTrack(trackIndex),
            () => _host.DetachAudio(clip));

        menu.Add(
            clip.Audio.Muted ? I18n.Menu.Unmute : I18n.Menu.Mute,
            "M",
            source is { HasAudio: true },
            () => _host.ToggleClipMute(clip));

        menu.Separator();
        menu.Add(I18n.Menu.Delete, "Del", true, _host.RemoveSelectedClip);
        menu.Add(I18n.Menu.RippleDelete, "Shift+Del", true, _host.RippleDeleteSelectedClip);
    }

    private void AddTrackItems(ContextMenu menu, int trackIndex)
    {
        menu.Add(
            I18n.Menu.AddTrack,
            "Ctrl+T",
            VideoTrackCount < Domain.Timeline.MaxVideoTracks,
            _host.AddTrack);

        menu.Add(
            I18n.Menu.AddAudioTrack,
            "Ctrl+Shift+A",
            _host.Timeline.AudioTrackCount < Domain.Timeline.MaxAudioTracks,
            _host.AddAudioTrack);

        menu.Add(
            I18n.Menu.RemoveTrack,
            "Ctrl+Shift+T",
            trackIndex > 0 && trackIndex < TrackCount,
            () => _host.RemoveTrack(trackIndex));
    }

    private void AddHistoryItems(ContextMenu menu)
    {
        menu.Add(I18n.Menu.Undo, "Ctrl+Z", _host.History.CanUndo, _host.Undo);
        menu.Add(I18n.Menu.Redo, "Ctrl+Y", _host.History.CanRedo, _host.Redo);
    }

    public override void OnMouseDown(Vector2f point, Mouse.Button button, bool doubleClick)
    {
        if (button == Mouse.Button.Right)
        {
            ShowMenu(point);
            return;
        }

        if (button != Mouse.Button.Left)
        {
            return;
        }

        if (_zoom.OnMouseDown(point))
        {
            return;
        }

        var body = BodyArea;
        if (point.Y < body.Top || point.Y > body.Top + body.Height)
        {
            return;
        }

        if (point.X < body.Left + Theme.TrackHeaderWidth)
        {
            foreach (var candidate in TrackButtons)
            {
                candidate.OnMouseDown(point);
            }

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

        var (clip, track, index, bounds) = hit.Value;
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
        _dragFromTrack = track;
        _dragToTrack = track;
        _dragFromIndex = index;
        _dragToIndex = index;
        _dragRejected = false;
        _dragStart = _host.Timeline.StartOf(clip);
        _dragGrabOffset = XToTime(point.X) - _dragStart;
    }

    private void BeginPlayheadDrag(Vector2f point)
    {
        _drag = DragMode.Playhead;
        _wasPlayingBeforeDrag = _host.Player.IsPlaying;
        SeekToPointer(point);
    }

    private void SeekToPointer(Vector2f point) =>
        _host.SeekTo(ClampToTimeline(PointerTime(point.X, null, false)), true);

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
        if (_drag == DragMode.None)
        {
            foreach (var button in TrackButtons)
            {
                button.UpdateHover(point);
            }
        }

        if (_zoom.OnMouseMove(point))
        {
            return;
        }

        switch (_drag)
        {
            case DragMode.Playhead:
                SeekToPointer(point);
                break;

            case DragMode.TrimIn:
            case DragMode.TrimOut:
                ApplyTrim(point);
                break;

            case DragMode.MoveClip:
                UpdateDropTarget(point);
                break;
        }
    }

    private void UpdateDropTarget(Vector2f point)
    {
        var track = TrackAt(point);
        _dragToTrack = track < 0 ? _dragFromTrack : track;
        _dragStart = DropStartFor(point);
        _dragToIndex = IsReorderDrag ? TargetIndexFor(point) : -1;
        _dragRejected = _dragClip is not null
                        && !EditRules.CanPlaceOnTrack(
                            _host.FindSource(_dragClip.SourceId),
                            KindOf(_dragToTrack));
    }

    private TimeSpan DropStartFor(Vector2f point)
    {
        var start = LaneGeometry.DropStart(XToTime(point.X), _dragGrabOffset);

        if (!SnapHeld || _dragClip is null)
        {
            return start;
        }

        return SnapGeometry.SnapSpan(
            start,
            _dragClip.Duration,
            SnapPoints(_dragClip, true),
            _pixelsPerSecond);
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
        var pointer = PointerTime(point.X, _dragClip, true);

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

        var handled = false;
        foreach (var candidate in TrackButtons)
        {
            handled |= candidate.OnMouseUp(point);
        }

        if (!handled)
        {
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
                    CommitMove();
                    break;
            }
        }

        _drag = DragMode.None;
        _dragClip = null;
        _dragFromTrack = -1;
        _dragToTrack = -1;
        _dragFromIndex = -1;
        _dragToIndex = -1;
        _dragRejected = false;
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

    private void CommitMove()
    {
        if (_dragClip is null || _dragFromTrack < 0 || _dragToTrack < 0 || _dragRejected)
        {
            return;
        }

        if (IsReorderDrag)
        {
            if (_dragToIndex >= 0 && _dragFromIndex >= 0 && _dragToIndex != _dragFromIndex)
            {
                _host.Execute(new ReorderClipCommand(_dragClip, _dragFromIndex, _dragToIndex));
            }

            return;
        }

        if (_dragToTrack == _dragFromTrack && _dragStart == _host.Timeline.StartOf(_dragClip))
        {
            return;
        }

        _host.MoveClipToTrack(_dragClip, _dragToTrack, _dragStart);
    }

    public override void OnScroll(Vector2f point, float delta, bool control)
    {
        if (control)
        {
            SetZoom(_pixelsPerSecond * (delta > 0 ? 1.18f : 1f / 1.18f), point.X);
            return;
        }

        var lane = LaneBounds;
        if (LaneGeometry.MaxScroll(lane.Height, TrackCount) > 0
            && point.Y >= lane.Top
            && point.Y <= lane.Top + lane.Height)
        {
            _laneScroll = LaneGeometry.ClampScroll(
                _laneScroll - (delta * LaneWheelStep),
                lane.Height,
                TrackCount);
            return;
        }

        _scrollSeconds -= delta * (60f / _pixelsPerSecond);
        ClampScroll();
    }

    public override void OnMouseLeave()
    {
        foreach (var button in TrackButtons)
        {
            button.UpdateHover(new Vector2f(-1, -1));
        }
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
