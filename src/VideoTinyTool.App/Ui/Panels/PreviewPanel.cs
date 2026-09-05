using SFML.Graphics;
using SFML.System;
using SFML.Window;
using VideoTinyTool.Domain;
using VideoTinyTool.Localization;
using VideoTinyTool.Media;
using VideoTinyTool.Ui.Widgets;

namespace VideoTinyTool.Ui.Panels;

public sealed class PreviewPanel : PanelBase
{
    private const float TransportPadding = 10f;
    private const float IconWidth = 26f;
    private const float IconHeight = 20f;
    private const float SliderHeight = 10f;
    private const float ValueWidth = 40f;
    private const float ResetHeight = 18f;
    private const float ScopePadding = 20f;

    private readonly IEditorHost _host;
    private readonly Button _toStart = new("|◀");
    private readonly Button _playPause = new("▶", ButtonStyle.Accent);
    private readonly Button _toEnd = new("▶|");
    private readonly Button _resetOverlay = new(string.Empty, ButtonStyle.Ghost);
    private readonly Slider _scrub = new();
    private readonly Slider _opacity = new();
    private readonly Slider _volume = new();

    private bool _wasPlayingBeforeScrub;
    private FloatRect _videoBounds;

    private Clip? _gestureClip;
    private OverlayHandle _handle;
    private OverlayTransform _gestureStart;
    private OverlayTransform _gestureTransform;
    private Clip? _audioClip;
    private ClipAudio _audioValue;
    private Vector2f _gestureOrigin;
    private double _gestureAspect = 16.0 / 9.0;

    public PreviewPanel(IEditorHost host)
    {
        _host = host;

        _toStart.Clicked += () => _host.SeekTo(TimeSpan.Zero, false);
        _toEnd.Clicked += () => _host.SeekTo(_host.Timeline.TotalDuration, false);
        _playPause.Clicked += () => _host.TogglePlayback();

        _scrub.ValueChanged += value =>
            _host.SeekTo(TimeSpan.FromTicks((long)(_host.Timeline.TotalDuration.Ticks * value)), true);
        _scrub.DragFinished += () => _host.EndScrub(_wasPlayingBeforeScrub);

        _opacity.ValueChanged += value => EditOverlay(transform => transform with { Opacity = value });
        _opacity.DragFinished += CommitOverlay;
        _volume.ValueChanged += value => EditAudio(audio => audio with { Volume = value });
        _volume.DragFinished += CommitAudio;
        _resetOverlay.Clicked += ResetOverlay;
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

    private FloatRect OverlayBarBounds => new(
        new Vector2f(Bounds.Left, Bounds.Top + Bounds.Height - Theme.TransportHeight - Theme.OverlayBarHeight),
        new Vector2f(Bounds.Width, Theme.OverlayBarHeight));

    private FloatRect FrameBounds
    {
        get
        {
            var footer = Theme.TransportHeight + (OverlayClip is null ? 0 : Theme.OverlayBarHeight);

            return new FloatRect(
                new Vector2f(Bounds.Left, Bounds.Top + Theme.PanelHeaderHeight),
                new Vector2f(Bounds.Width, Bounds.Height - Theme.PanelHeaderHeight - footer));
        }
    }

    private Clip? OverlayClip =>
        _host.SelectedClip is { } clip
        && _host.SelectedTrackIndex > 0
        && !_host.Timeline.IsAudioTrack(_host.SelectedTrackIndex)
            ? clip
            : null;

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

        LayoutOverlayBar(renderer);
    }

    private void LayoutOverlayBar(Renderer renderer)
    {
        _resetOverlay.Label = I18n.Preview.OverlayReset;

        var bar = OverlayBarBounds;
        var resetWidth = _resetOverlay.PreferredWidth(renderer, Theme.FontSizeLabel);

        _resetOverlay.Bounds = new FloatRect(
            new Vector2f(
                MathF.Round(bar.Left + bar.Width - Theme.Padding - resetWidth),
                MathF.Round(bar.Top + ((bar.Height - ResetHeight) / 2f))),
            new Vector2f(resetWidth, ResetHeight));

        var labelWidth = Math.Max(
            renderer.MeasureText(I18n.Preview.OverlayOpacity, Theme.FontSizeLabel),
            renderer.MeasureText(I18n.Preview.OverlayVolume, Theme.FontSizeLabel)) + 10f;

        var available = bar.Width - (Theme.Padding * 2f) - resetWidth - 12f;
        var groupWidth = Math.Max(labelWidth + ValueWidth + 30f, (available - 16f) / 2f);
        var trackWidth = Math.Max(24f, groupWidth - labelWidth - ValueWidth);
        var trackTop = bar.Top + ((bar.Height - SliderHeight) / 2f);

        var left = bar.Left + Theme.Padding;
        _opacity.Bounds = new FloatRect(
            new Vector2f(MathF.Round(left + labelWidth), MathF.Round(trackTop)),
            new Vector2f(trackWidth, SliderHeight));

        left += groupWidth + 16f;
        _volume.Bounds = new FloatRect(
            new Vector2f(MathF.Round(left + labelWidth), MathF.Round(trackTop)),
            new Vector2f(trackWidth, SliderHeight));
    }

    private string TimecodeText() =>
        $"{TimeFormat.Timecode(_host.Player.Position)} / {TimeFormat.Timecode(_host.Timeline.TotalDuration)}";

    public override void Draw(Renderer renderer)
    {
        renderer.FillRect(Bounds, Theme.Sunk);

        var meta = _host.Timeline.IsAudioOnly
            ? I18n.Preview.AudioOnly
            : OverlayClip is null
                ? I18n.Preview.Format(
                    _host.Settings.Export.Width,
                    _host.Settings.Export.Height,
                    _host.Settings.Export.FrameRate)
                : I18n.Preview.OverlayHint;

        DrawHeader(renderer, I18n.Preview.Title, meta);
        DrawFrame(renderer);

        if (OverlayClip is not null)
        {
            DrawOverlayBar(renderer);
        }

        DrawTransport(renderer);
    }

    private void DrawFrame(Renderer renderer)
    {
        var frame = FrameBounds;
        renderer.FillRect(frame, Theme.FrameVoid);

        if (!_host.Timeline.HasClips)
        {
            _videoBounds = default;
            renderer.DrawTextCentered(I18n.Preview.EmptyTimeline, frame, Theme.FontSizeBody, Theme.TextFaint);
            return;
        }

        if (_host.Timeline.IsAudioOnly)
        {
            DrawAudioScope(renderer, frame);
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

        _videoBounds = target;

        var texture = _host.Player.InGap ? null : _host.Player.CurrentTexture;
        if (texture is not null)
        {
            renderer.DrawTexture(texture, target);
        }

        DrawOverlays(renderer, target);
        DrawOverlaySelection(renderer, target);

        if (texture is null && !_host.Player.InGap && _host.Player.Overlays.Count == 0)
        {
            renderer.DrawTextCentered(I18n.Preview.PreparingFrame, frame, Theme.FontSizeBody, Theme.TextFaint);
            return;
        }

        if (Math.Abs(_host.Player.Rate - 1.0) > 0.001 && _host.Player.IsPlaying)
        {
            var badge = new FloatRect(
                new Vector2f(frame.Left + 16, frame.Top + 16),
                new Vector2f(42, 18));

            renderer.FillRect(badge, Theme.Shade);
            renderer.DrawTextCentered(
                I18n.Preview.RateBadge(_host.Player.Rate),
                badge,
                Theme.FontSizeLabel,
                Theme.Accent,
                TextFont.Mono);
        }
    }

    private void DrawAudioScope(Renderer renderer, FloatRect frame)
    {
        _videoBounds = default;

        var inner = new FloatRect(
            new Vector2f(frame.Left + ScopePadding, frame.Top + ScopePadding),
            new Vector2f(
                Math.Max(1f, frame.Width - (ScopePadding * 2f)),
                Math.Max(1f, frame.Height - (ScopePadding * 2f))));

        var centre = MathF.Round(inner.Top + (inner.Height / 2f));
        renderer.HorizontalLine(inner.Left, centre, inner.Width, Theme.Line);

        if (AudioAtPlayhead() is not { } located)
        {
            renderer.DrawTextCentered(I18n.Preview.AudioSilence, frame, Theme.FontSizeBody, Theme.TextFaint);
            return;
        }

        var (clip, source) = located;
        var seconds = clip.Duration.TotalSeconds;
        if (seconds <= 0)
        {
            return;
        }

        var peaks = _host.Waveforms.Get(source);
        var half = MathF.Max(1f, (inner.Height / 2f) - 1f);
        var gain = clip.Audio.Gain;

        if (peaks is { Length: > 0 } && gain > 0f)
        {
            var pixelsPerSecond = (float)(inner.Width / seconds);
            var span = peaks.AsSpan();

            renderer.PushClip(inner);

            for (var column = 0; column < (int)inner.Width; column++)
            {
                var (from, to) = WaveformGeometry.ColumnRange(clip.In, column, 1f, pixelsPerSecond);
                var peak = WaveformGeometry.PeakBetween(
                    span,
                    WaveformGeometry.BucketAt(from, WaveformService.BucketsPerSecond),
                    WaveformGeometry.BucketAt(to, WaveformService.BucketsPerSecond));

                var amplitude = peak / 255f * gain * half;
                if (amplitude < 0.5f)
                {
                    continue;
                }

                renderer.FillRect(inner.Left + column, centre - amplitude, 1f, amplitude * 2f, Theme.Waveform);
            }

            renderer.PopClip();
        }

        var offset = (_host.Player.Position - _host.Timeline.StartOf(clip)).TotalSeconds;
        var cursor = MathF.Round(inner.Left + (float)(Math.Clamp(offset / seconds, 0, 1) * inner.Width));
        renderer.VerticalLine(cursor, inner.Top, inner.Height, Theme.Accent);

        renderer.DrawText(
            renderer.Ellipsize(source.FileName, inner.Width, Theme.FontSizeLabel),
            inner.Left,
            frame.Top + 6f,
            Theme.FontSizeLabel,
            Theme.TextDim);
    }

    private (Clip Clip, MediaSource Source)? AudioAtPlayhead()
    {
        var timeline = _host.Timeline;
        var position = _host.Player.Position;

        if (timeline.IsAudioTrack(_host.SelectedTrackIndex)
            && AudioOnTrack(_host.SelectedTrackIndex, position) is { } preferred)
        {
            return preferred;
        }

        for (var track = timeline.FirstAudioTrackIndex; track < timeline.Tracks.Count; track++)
        {
            if (AudioOnTrack(track, position) is { } found)
            {
                return found;
            }
        }

        return null;
    }

    private (Clip Clip, MediaSource Source)? AudioOnTrack(int trackIndex, TimeSpan position)
    {
        if (_host.Timeline.Resolve(trackIndex, position) is not { } location)
        {
            return null;
        }

        var source = _host.FindSource(location.Clip.SourceId);
        return source is null ? null : (location.Clip, source);
    }

    private void DrawOverlays(Renderer renderer, FloatRect video)
    {
        foreach (var overlay in _host.Player.Overlays)
        {
            var transform = LiveTransform(overlay.TrackIndex, overlay.Transform);
            var box = OverlayGeometry.Project(transform, video.Width, video.Height, overlay.SourceAspect);

            var bounds = new FloatRect(
                new Vector2f(MathF.Round(video.Left + box.Left), MathF.Round(video.Top + box.Top)),
                new Vector2f(MathF.Round(box.Width), MathF.Round(box.Height)));

            renderer.DrawTexture(
                overlay.Texture,
                bounds,
                new Color(255, 255, 255, (byte)Math.Clamp(transform.Opacity * 255f, 0f, 255f)));
        }
    }

    private void DrawOverlaySelection(Renderer renderer, FloatRect video)
    {
        if (OverlayClip is not { } clip || !VisibleAtPlayhead(clip))
        {
            return;
        }

        var box = OverlayGeometry.Project(
            CurrentTransform(clip),
            video.Width,
            video.Height,
            AspectOf(clip, _host.SelectedTrackIndex));

        var bounds = new FloatRect(
            new Vector2f(MathF.Round(video.Left + box.Left), MathF.Round(video.Top + box.Top)),
            new Vector2f(MathF.Round(box.Width), MathF.Round(box.Height)));

        renderer.StrokeRect(bounds, Theme.Accent, 2f);

        foreach (var corner in Corners(bounds))
        {
            renderer.FillRect(
                new FloatRect(
                    new Vector2f(
                        corner.X - (OverlayGeometry.HandleSize / 2f),
                        corner.Y - (OverlayGeometry.HandleSize / 2f)),
                    new Vector2f(OverlayGeometry.HandleSize, OverlayGeometry.HandleSize)),
                Theme.Accent);
        }
    }

    private static IEnumerable<Vector2f> Corners(FloatRect bounds)
    {
        yield return new Vector2f(bounds.Left, bounds.Top);
        yield return new Vector2f(bounds.Left + bounds.Width, bounds.Top);
        yield return new Vector2f(bounds.Left, bounds.Top + bounds.Height);
        yield return new Vector2f(bounds.Left + bounds.Width, bounds.Top + bounds.Height);
    }

    private void DrawOverlayBar(Renderer renderer)
    {
        var bar = OverlayBarBounds;
        renderer.FillRect(bar, Theme.Chrome);
        renderer.HorizontalLine(bar.Left, bar.Top, bar.Width, Theme.Line);

        if (OverlayClip is not { } clip)
        {
            return;
        }

        var transform = CurrentTransform(clip);

        if (!_opacity.Dragging)
        {
            _opacity.Value = transform.Opacity;
        }

        var audio = CurrentAudio(clip);

        if (!_volume.Dragging)
        {
            _volume.Value = audio.Volume;
        }

        DrawSliderGroup(renderer, bar, I18n.Preview.OverlayOpacity, _opacity, transform.Opacity);
        DrawSliderGroup(renderer, bar, I18n.Preview.OverlayVolume, _volume, audio.Volume);

        _resetOverlay.Draw(renderer);
    }

    private static void DrawSliderGroup(Renderer renderer, FloatRect bar, string label, Slider slider, float value)
    {
        var textTop = bar.Top + ((bar.Height - 14f) / 2f);

        renderer.DrawText(
            label,
            slider.Bounds.Left - 8f,
            textTop,
            Theme.FontSizeLabel,
            Theme.TextDim,
            TextFont.Regular,
            TextAlign.Right);

        slider.Draw(renderer);

        renderer.DrawText(
            I18n.Preview.OverlayPercent(value),
            slider.Bounds.Left + slider.Bounds.Width + 8f,
            textTop,
            Theme.FontSizeLabel,
            Theme.TextDim,
            TextFont.Mono);
    }

    private void DrawTransport(Renderer renderer)
    {
        var transport = TransportBounds;
        renderer.FillRect(transport, Theme.Chrome);
        renderer.HorizontalLine(transport.Left, transport.Top, transport.Width, Theme.Line);

        var hasClips = _host.Timeline.HasClips;
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

    private bool VisibleAtPlayhead(Clip clip) =>
        _host.Timeline.Resolve(_host.SelectedTrackIndex, _host.Player.Position) is { } location
        && ReferenceEquals(location.Clip, clip);

    private double AspectOf(Clip clip, int trackIndex)
    {
        foreach (var overlay in _host.Player.Overlays)
        {
            if (overlay.TrackIndex == trackIndex && overlay.SourceAspect > 0)
            {
                return overlay.SourceAspect;
            }
        }

        return _host.FindSource(clip.SourceId)?.AspectRatio ?? 16.0 / 9.0;
    }

    private OverlayTransform CurrentTransform(Clip clip) =>
        ReferenceEquals(_gestureClip, clip) ? _gestureTransform : clip.Overlay;

    private ClipAudio CurrentAudio(Clip clip) =>
        ReferenceEquals(_audioClip, clip) ? _audioValue : clip.Audio;

    private OverlayTransform LiveTransform(int trackIndex, OverlayTransform fallback) =>
        _gestureClip is { } clip && _host.Timeline.TrackIndexOf(clip) == trackIndex
            ? _gestureTransform
            : fallback;

    private void EditOverlay(Func<OverlayTransform, OverlayTransform> change)
    {
        if (_gestureClip is null)
        {
            if (OverlayClip is not { } clip)
            {
                return;
            }

            _gestureClip = clip;
            _gestureStart = clip.Overlay;
            _gestureTransform = clip.Overlay;
            _handle = OverlayHandle.None;
        }

        _gestureTransform = change(_gestureTransform).Clamped();
    }

    private void EditAudio(Func<ClipAudio, ClipAudio> change)
    {
        if (_audioClip is null)
        {
            if (OverlayClip is not { } clip)
            {
                return;
            }

            _audioClip = clip;
            _audioValue = clip.Audio;
        }

        _audioValue = change(_audioValue).Clamped();
    }

    private void CommitAudio()
    {
        var clip = _audioClip;
        var audio = _audioValue;

        _audioClip = null;

        if (clip is null || audio == clip.Audio)
        {
            return;
        }

        _host.SetClipAudio(clip, audio);
    }

    private void CommitOverlay()
    {
        var clip = _gestureClip;
        var transform = _gestureTransform;

        _gestureClip = null;
        _handle = OverlayHandle.None;

        if (clip is null || transform == clip.Overlay)
        {
            return;
        }

        _host.SetOverlayTransform(clip, transform);
    }

    private void ResetOverlay()
    {
        if (OverlayClip is not { } clip || clip.Overlay == OverlayTransform.Default)
        {
            return;
        }

        _host.SetOverlayTransform(clip, OverlayTransform.Default);
    }

    private bool BeginOverlayGesture(Vector2f point)
    {
        if (OverlayClip is not { } clip || !VisibleAtPlayhead(clip) || _videoBounds.Width <= 0)
        {
            return false;
        }

        var aspect = AspectOf(clip, _host.SelectedTrackIndex);
        var box = OverlayGeometry.Project(clip.Overlay, _videoBounds.Width, _videoBounds.Height, aspect);
        var handle = OverlayGeometry.HitTest(box, point.X - _videoBounds.Left, point.Y - _videoBounds.Top);

        if (handle == OverlayHandle.None)
        {
            return false;
        }

        _gestureClip = clip;
        _gestureStart = clip.Overlay;
        _gestureTransform = clip.Overlay;
        _gestureOrigin = point;
        _gestureAspect = aspect;
        _handle = handle;
        return true;
    }

    private void UpdateOverlayGesture(Vector2f point)
    {
        var deltaX = point.X - _gestureOrigin.X;
        var deltaY = point.Y - _gestureOrigin.Y;

        _gestureTransform = _handle == OverlayHandle.Body
            ? OverlayGeometry.Move(_gestureStart, _videoBounds.Width, _videoBounds.Height, deltaX, deltaY)
            : OverlayGeometry.Resize(
                _gestureStart,
                _handle,
                _videoBounds.Width,
                _videoBounds.Height,
                _gestureAspect,
                deltaX,
                deltaY);
    }

    public override void OnMouseMove(Vector2f point)
    {
        foreach (var button in Buttons)
        {
            button.UpdateHover(point);
        }

        _resetOverlay.UpdateHover(point);

        if (_handle != OverlayHandle.None)
        {
            UpdateOverlayGesture(point);
            return;
        }

        if (_opacity.OnMouseMove(point) || _volume.OnMouseMove(point))
        {
            return;
        }

        _scrub.OnMouseMove(point);
    }

    private void ShowMenu(Vector2f point)
    {
        var hasClips = _host.Timeline.HasClips;
        var menu = new ContextMenu(point);

        menu.Add(
            _host.Player.IsPlaying ? I18n.Menu.Pause : I18n.Menu.Play,
            "Space",
            hasClips,
            _host.TogglePlayback);

        menu.Add(I18n.Menu.GoToStart, "Home", hasClips, () => _host.SeekTo(TimeSpan.Zero, false));
        menu.Add(I18n.Menu.GoToEnd, "End", hasClips, () => _host.SeekTo(_host.Timeline.TotalDuration, false));
        menu.Separator();
        menu.Add(I18n.Menu.Split, "S", _host.SelectedClip is not null, _host.SplitAtPlayhead);
        menu.Add(I18n.Menu.ExportFrame, "Ctrl+Shift+M", _host.Timeline.HasVideoClips, _host.ExportFrame);

        if (OverlayClip is { } clip)
        {
            menu.Add(
                I18n.Menu.ResetOverlay,
                string.Empty,
                clip.Overlay != OverlayTransform.Default,
                ResetOverlay);
        }

        menu.Separator();
        menu.Add(I18n.Menu.Shortcuts, "F1", true, _host.ShowShortcuts);

        _host.ShowContextMenu(menu);
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

        foreach (var candidate in Buttons)
        {
            if (candidate.OnMouseDown(point))
            {
                return;
            }
        }

        if (OverlayClip is not null
            && (_resetOverlay.OnMouseDown(point) || _opacity.OnMouseDown(point) || _volume.OnMouseDown(point)))
        {
            return;
        }

        if (!_host.Timeline.HasClips)
        {
            return;
        }

        if (FrameBounds.Contains(point))
        {
            if (!BeginOverlayGesture(point))
            {
                _host.TogglePlayback();
            }

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

        if (_handle != OverlayHandle.None)
        {
            CommitOverlay();
            return;
        }

        if (_opacity.OnMouseUp() || _volume.OnMouseUp() || _scrub.OnMouseUp())
        {
            return;
        }

        if (_resetOverlay.OnMouseUp(point))
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

        _resetOverlay.UpdateHover(new Vector2f(-1, -1));
    }
}
