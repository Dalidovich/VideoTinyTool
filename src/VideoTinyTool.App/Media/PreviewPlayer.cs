using SFML.Audio;
using SFML.Graphics;
using SFML.System;
using VideoTinyTool.Domain;

namespace VideoTinyTool.Media;

public readonly record struct OverlayFrame(int TrackIndex, Texture Texture, OverlayTransform Transform, double SourceAspect);

public sealed class PreviewPlayer : IDisposable
{
    private static readonly TimeSpan PrefetchLead = TimeSpan.FromMilliseconds(500);
    private static readonly TimeSpan ResumeDelay = TimeSpan.FromMilliseconds(150);
    private static readonly TimeSpan PrimeTimeout = TimeSpan.FromMilliseconds(600);

    private const int MinimumOverlayWidth = 160;

    private readonly Timeline _timeline;
    private readonly Func<Guid, MediaSource?> _sourceLookup;
    private readonly StillFrameService _stillFrames;
    private readonly PlaybackClock _clock = new();
    private readonly PcmRingBuffer _silentRing;
    private readonly PcmSoundStream _sound;
    private readonly PcmMixSource _mixer = new();
    private readonly List<(IPcmSource Source, float Gain)> _mixInputs = new();
    private readonly List<OverlayTrackState> _overlayTracks = new();
    private readonly List<OverlayFrame> _overlayFrames = new();
    private readonly int _previewWidth;
    private readonly int _previewHeight;
    private readonly Texture _liveTexture;

    private Texture? _stillTexture;
    private ClipPipeline? _current;
    private ClipPipeline? _next;

    private TimeSpan _pausedPosition = TimeSpan.Zero;
    private TimeSpan _audioAnchor = TimeSpan.Zero;
    private TimeSpan _globalAnchor = TimeSpan.Zero;
    private TimeSpan _gapEnd = TimeSpan.Zero;
    private DateTime? _resumeAt;
    private Guid _stillToken;
    private double _rate = 1.0;
    private bool _scrubbing;
    private bool _liveFrameValid;
    private bool _inGap;
    private bool _disposed;

    public PreviewPlayer(
        Timeline timeline,
        Func<Guid, MediaSource?> sourceLookup,
        StillFrameService stillFrames,
        int previewWidth,
        int previewHeight)
    {
        _timeline = timeline;
        _sourceLookup = sourceLookup;
        _stillFrames = stillFrames;
        _previewWidth = previewWidth;
        _previewHeight = previewHeight;

        _liveTexture = new Texture(new Vector2u((uint)previewWidth, (uint)previewHeight)) { Smooth = true };
        _silentRing = new PcmRingBuffer(
            AudioPcmPipe.SampleRate * AudioPcmPipe.Channels * AudioPcmPipe.BytesPerSample / 4);
        _sound = new PcmSoundStream(_silentRing);

        SyncOverlayTracks();
    }

    public bool IsPlaying { get; private set; }

    public double Rate => _rate;

    public double SourceAspect { get; private set; } = 16.0 / 9.0;

    public bool InGap => _inGap;

    public Texture? CurrentTexture => _inGap ? null : _liveFrameValid ? _liveTexture : _stillTexture;

    public IReadOnlyList<OverlayFrame> Overlays
    {
        get
        {
            _overlayFrames.Clear();
            foreach (var track in _overlayTracks)
            {
                var texture = track.Texture;
                if (texture is not null)
                {
                    _overlayFrames.Add(new OverlayFrame(track.TrackIndex, texture, track.Transform, track.Aspect));
                }
            }

            return _overlayFrames;
        }
    }

    public TimeSpan Duration => _timeline.TotalDuration;

    public TimeSpan Position
    {
        get
        {
            if (!IsPlaying)
            {
                return _pausedPosition;
            }

            var position = _clock.AudioDriven
                ? _globalAnchor + (_sound.PlayingOffset.ToTimeSpan() - _audioAnchor)
                : _clock.Position;

            return Clamp(position);
        }
    }

    public void Play()
    {
        if (IsPlaying || _timeline.Clips.Count == 0)
        {
            return;
        }

        _rate = 1.0;
        StartPlayback(_pausedPosition, withAudio: true);
    }

    public void PlayAtRate(double rate)
    {
        if (_timeline.Clips.Count == 0)
        {
            return;
        }

        var position = Position;
        Stop(keepPosition: true);
        _rate = rate;
        StartPlayback(position, withAudio: IsNormalRate(rate));
    }

    public void Pause()
    {
        if (!IsPlaying)
        {
            return;
        }

        var position = Position;
        Stop(keepPosition: true);
        _rate = 1.0;
        _pausedPosition = Clamp(position);
        RequestStillFrame(_pausedPosition);
    }

    public void TogglePlay()
    {
        if (IsPlaying)
        {
            Pause();
        }
        else
        {
            Play();
        }
    }

    public void Seek(TimeSpan position, bool scrubbing)
    {
        var target = Clamp(position);
        var wasPlaying = IsPlaying;

        Stop(keepPosition: false);
        _pausedPosition = target;
        _scrubbing = scrubbing;

        RequestStillFrame(target);

        _resumeAt = scrubbing || !wasPlaying ? null : DateTime.UtcNow + ResumeDelay;
    }

    public void EndScrub(bool resumePlayback)
    {
        if (!_scrubbing)
        {
            return;
        }

        _scrubbing = false;
        _resumeAt = resumePlayback ? DateTime.UtcNow + ResumeDelay : null;
    }

    public void TimelineChanged()
    {
        var position = Clamp(Position);
        var wasPlaying = IsPlaying;

        Stop(keepPosition: false);
        SyncOverlayTracks();
        _pausedPosition = position;

        if (_timeline.Clips.Count == 0)
        {
            _liveFrameValid = false;
            _inGap = false;
            _stillTexture?.Dispose();
            _stillTexture = null;

            foreach (var track in _overlayTracks)
            {
                track.ClearFrames();
            }

            return;
        }

        RequestStillFrame(position);
        _resumeAt = wasPlaying ? DateTime.UtcNow + ResumeDelay : null;
    }

    public void Update()
    {
        PumpStillFrames();

        if (_resumeAt is { } resumeAt && DateTime.UtcNow >= resumeAt)
        {
            _resumeAt = null;
            StartPlayback(_pausedPosition, withAudio: IsNormalRate(_rate));
        }

        if (!IsPlaying)
        {
            return;
        }

        var position = Position;

        if (position >= Duration)
        {
            Stop(keepPosition: false);
            _rate = 1.0;
            _pausedPosition = Duration;
            RequestStillFrame(Duration - TimeSpan.FromMilliseconds(40));
            return;
        }

        UpdateOverlays(position);

        if (_inGap)
        {
            if (position >= _gapEnd)
            {
                StartPlayback(_gapEnd, withAudio: IsNormalRate(_rate));
            }

            return;
        }

        AdvanceFrames(position);
        Prefetch(position);
        SwitchPipelineIfNeeded(position);
    }

    private void AdvanceFrames(TimeSpan position)
    {
        var pipeline = _current;
        if (pipeline is null)
        {
            return;
        }

        var guard = 0;
        while (guard++ < 8 && pipeline.NextFrameGlobalTime <= position && pipeline.TryTakeFrame(out var frame))
        {
            _liveTexture.Update(frame);
            _liveFrameValid = true;
            SourceAspect = pipeline.Source.AspectRatio;
        }
    }

    private void Prefetch(TimeSpan position)
    {
        var pipeline = _current;
        if (pipeline is null || _next is not null)
        {
            return;
        }

        if (position < pipeline.GlobalEnd - PrefetchLead)
        {
            return;
        }

        if (_timeline.NextClipStart(pipeline.GlobalEnd) > pipeline.GlobalEnd)
        {
            return;
        }

        _next = CreatePipeline(pipeline.ClipIndex + 1, pipeline.GlobalEnd, IsNormalRate(_rate));
        _next?.Start();
    }

    private void SwitchPipelineIfNeeded(TimeSpan position)
    {
        var pipeline = _current;
        if (pipeline is null || position < pipeline.GlobalEnd)
        {
            return;
        }

        var nextStart = _timeline.NextClipStart(pipeline.GlobalEnd);
        if (nextStart > pipeline.GlobalEnd)
        {
            EnterGap(pipeline.GlobalEnd, nextStart);
            return;
        }

        pipeline.Dispose();
        _current = _next;
        _next = null;

        _current ??= CreatePipeline(pipeline.ClipIndex + 1, pipeline.GlobalEnd, IsNormalRate(_rate));
        _current?.Start();

        RefreshAudioMix();
    }

    private void EnterGap(TimeSpan from, TimeSpan until)
    {
        DisposeBasePipelines();

        if (HasOverlayAudio)
        {
            RefreshAudioMix();
        }
        else
        {
            SilenceAudio();
        }

        _gapEnd = until;
        _inGap = true;
        _liveFrameValid = false;
        _stillToken = Guid.NewGuid();

        _clock.StartSystem(from, _rate);
        IsPlaying = true;
        _resumeAt = null;
    }

    private void StartPlayback(TimeSpan position, bool withAudio)
    {
        if (_timeline.Clips.Count == 0)
        {
            return;
        }

        var target = Clamp(position);
        if (target >= Duration)
        {
            target = TimeSpan.Zero;
        }

        var location = _timeline.Resolve(target);
        if (location is null)
        {
            EnterGap(target, _timeline.NextClipStart(target));
            return;
        }

        var source = _sourceLookup(location.Value.Clip.SourceId);
        if (source is null)
        {
            _pausedPosition = target;
            return;
        }

        DisposeBasePipelines();
        _inGap = false;

        _current = new ClipPipeline(
            location.Value.Clip,
            source,
            location.Value.Index,
            target,
            location.Value.SourceOffset,
            _previewWidth,
            _previewHeight,
            withAudio);
        _current.Start();

        var deadline = DateTime.UtcNow + PrimeTimeout;
        while (!_current.Primed && !_current.Exhausted && DateTime.UtcNow < deadline)
        {
            Thread.Sleep(4);
        }

        _globalAnchor = target;
        IsPlaying = true;
        _resumeAt = null;

        if (withAudio)
        {
            _silentRing.Clear();
            RefreshAudioMix();

            if (_sound.Status != SoundStatus.Playing)
            {
                _sound.Play();
            }

            _audioAnchor = _sound.PlayingOffset.ToTimeSpan();
            _clock.StartAudio(target, () => _sound.PlayingOffset.ToTimeSpan() - _audioAnchor);
        }
        else
        {
            _clock.StartSystem(target, _rate);
        }
    }

    private ClipPipeline? CreatePipeline(int clipIndex, TimeSpan globalStart, bool withAudio)
    {
        if (clipIndex < 0 || clipIndex >= _timeline.Clips.Count)
        {
            return null;
        }

        var clip = _timeline.Clips[clipIndex];
        var source = _sourceLookup(clip.SourceId);
        if (source is null)
        {
            return null;
        }

        return new ClipPipeline(
            clip,
            source,
            clipIndex,
            globalStart,
            clip.In,
            _previewWidth,
            _previewHeight,
            withAudio);
    }

    private void SyncOverlayTracks()
    {
        var wanted = _timeline.VideoTrackCount - 1;

        while (_overlayTracks.Count > wanted)
        {
            var last = _overlayTracks[^1];
            _overlayTracks.RemoveAt(_overlayTracks.Count - 1);
            last.Dispose();
        }

        while (_overlayTracks.Count < wanted)
        {
            _overlayTracks.Add(new OverlayTrackState(_overlayTracks.Count + 1));
        }
    }

    private void UpdateOverlays(TimeSpan position)
    {
        if (_overlayTracks.Count == 0)
        {
            return;
        }

        var withAudio = IsNormalRate(_rate);
        var rewired = false;

        foreach (var track in _overlayTracks)
        {
            rewired |= SyncOverlayPipeline(track, position, withAudio);
            AdvanceOverlayFrames(track, position);
            PrefetchOverlay(track, position, withAudio);
        }

        if (rewired)
        {
            RefreshAudioMix();
        }
    }

    private bool SyncOverlayPipeline(OverlayTrackState track, TimeSpan position, bool withAudio)
    {
        var location = _timeline.Resolve(track.TrackIndex, position);

        if (location is null)
        {
            if (track.Current is null)
            {
                return false;
            }

            track.Current.Dispose();
            track.Current = null;
            track.ClearFrames();
            return true;
        }

        var clip = location.Value.Clip;
        track.Transform = clip.Overlay;

        if (track.Current is not null && ReferenceEquals(track.Current.Clip, clip))
        {
            return false;
        }

        if (track.Next is not null && ReferenceEquals(track.Next.Clip, clip))
        {
            track.Current?.Dispose();
            track.Current = track.Next;
            track.Next = null;
            return true;
        }

        track.Next?.Dispose();
        track.Next = null;
        track.Current?.Dispose();
        track.Current = CreateOverlayPipeline(clip, location.Value.Index, position, location.Value.SourceOffset, withAudio);
        track.Current?.Start();
        return true;
    }

    private void AdvanceOverlayFrames(OverlayTrackState track, TimeSpan position)
    {
        var pipeline = track.Current;
        if (pipeline is null)
        {
            return;
        }

        var guard = 0;
        while (guard++ < 8 && pipeline.NextFrameGlobalTime <= position && pipeline.TryTakeFrame(out var frame))
        {
            track.EnsureTexture(pipeline.Width, pipeline.Height).Update(frame);
            track.LiveValid = true;
            track.Aspect = pipeline.Source.AspectRatio;
        }
    }

    private void PrefetchOverlay(OverlayTrackState track, TimeSpan position, bool withAudio)
    {
        var pipeline = track.Current;
        if (pipeline is null || track.Next is not null)
        {
            return;
        }

        if (position < pipeline.GlobalEnd - PrefetchLead)
        {
            return;
        }

        if (_timeline.NextClipStart(track.TrackIndex, pipeline.GlobalEnd) > pipeline.GlobalEnd)
        {
            return;
        }

        var clips = _timeline.ClipsOf(track.TrackIndex);
        var index = pipeline.ClipIndex + 1;
        if (index >= clips.Count)
        {
            return;
        }

        track.Next = CreateOverlayPipeline(clips[index], index, pipeline.GlobalEnd, clips[index].In, withAudio);
        track.Next?.Start();
    }

    private ClipPipeline? CreateOverlayPipeline(
        Clip clip,
        int clipIndex,
        TimeSpan globalStart,
        TimeSpan sourceOffset,
        bool withAudio)
    {
        var source = _sourceLookup(clip.SourceId);
        if (source is null)
        {
            return null;
        }

        var size = OverlaySize(clip.Overlay);

        return new ClipPipeline(
            clip,
            source,
            clipIndex,
            globalStart,
            sourceOffset,
            size.Width,
            size.Height,
            withAudio);
    }

    private (int Width, int Height) OverlaySize(OverlayTransform transform)
    {
        var width = Even((int)Math.Round(_previewWidth * (double)transform.Width, MidpointRounding.AwayFromZero));
        if (width < MinimumOverlayWidth)
        {
            width = Even(MinimumOverlayWidth);
        }

        if (width > _previewWidth)
        {
            width = Even(_previewWidth);
        }

        var height = Even((int)Math.Round(_previewHeight * (double)width / _previewWidth, MidpointRounding.AwayFromZero));
        return (width, Math.Min(height, Even(_previewHeight)));
    }

    private static int Even(int value) => value < 2 ? 2 : value % 2 == 0 ? value : value + 1;

    private bool HasOverlayAudio
    {
        get
        {
            foreach (var track in _overlayTracks)
            {
                if (track.Current?.Ring is not null)
                {
                    return true;
                }
            }

            return false;
        }
    }

    private void RefreshAudioMix()
    {
        _mixInputs.Clear();

        foreach (var track in _overlayTracks)
        {
            if (track.Current is { Ring: { } ring } pipeline)
            {
                _mixInputs.Add((ring, pipeline.Clip.Audio.Gain));
            }
        }

        if (_mixInputs.Count == 0)
        {
            _sound.Source = _current?.Ring ?? _silentRing;
            return;
        }

        if (_current?.Ring is { } baseRing)
        {
            _mixInputs.Insert(0, (baseRing, 1f));
        }

        _mixer.SetInputs(_mixInputs);
        _sound.Source = _mixer;

        if (IsPlaying && IsNormalRate(_rate) && _sound.Status != SoundStatus.Playing)
        {
            _sound.Play();
        }
    }

    private void Stop(bool keepPosition)
    {
        if (IsPlaying && keepPosition)
        {
            _pausedPosition = Clamp(Position);
        }

        IsPlaying = false;
        _resumeAt = null;
        _clock.Reset(_pausedPosition);

        SilenceAudio();
        DisposePipelines();
    }

    private void SilenceAudio()
    {
        try
        {
            _sound.Stop();
        }
        catch (Exception)
        {
            // The audio device may already be gone.
        }

        _sound.Source = _silentRing;
        _silentRing.Clear();
    }

    private void DisposePipelines()
    {
        DisposeBasePipelines();

        foreach (var track in _overlayTracks)
        {
            track.DisposePipelines();
        }
    }

    private void DisposeBasePipelines()
    {
        _current?.Dispose();
        _current = null;
        _next?.Dispose();
        _next = null;
    }

    private void RequestStillFrame(TimeSpan position)
    {
        if (_timeline.Clips.Count == 0)
        {
            _liveFrameValid = false;
            _inGap = false;
            return;
        }

        var probe = position >= Duration ? Duration - TimeSpan.FromMilliseconds(40) : position;
        if (probe < TimeSpan.Zero)
        {
            probe = TimeSpan.Zero;
        }

        RequestOverlayStills(probe);

        var location = _timeline.Resolve(probe);
        if (location is null)
        {
            _inGap = true;
            _liveFrameValid = false;
            _stillToken = Guid.NewGuid();
            return;
        }

        var source = _sourceLookup(location.Value.Clip.SourceId);
        if (source is null)
        {
            return;
        }

        _inGap = false;
        _stillToken = _stillFrames.Request(source, location.Value.SourceOffset, _previewHeight);
        SourceAspect = source.AspectRatio;
    }

    private void RequestOverlayStills(TimeSpan probe)
    {
        foreach (var track in _overlayTracks)
        {
            var location = _timeline.Resolve(track.TrackIndex, probe);
            if (location is null)
            {
                track.StillToken = Guid.NewGuid();
                track.ClearFrames();
                continue;
            }

            var source = _sourceLookup(location.Value.Clip.SourceId);
            if (source is null)
            {
                continue;
            }

            track.Transform = location.Value.Clip.Overlay;
            track.StillToken = _stillFrames.Request(
                track.TrackIndex,
                source,
                location.Value.SourceOffset,
                OverlaySize(location.Value.Clip.Overlay).Height);
        }
    }

    private void PumpStillFrames()
    {
        while (_stillFrames.TakeResult() is { } result)
        {
            if (result.Png is not { Length: > 0 })
            {
                continue;
            }

            if (result.Slot == 0)
            {
                ApplyBaseStill(result);
                continue;
            }

            ApplyOverlayStill(result);
        }
    }

    private void ApplyBaseStill(StillFrame result)
    {
        if (result.Token != _stillToken)
        {
            return;
        }

        var texture = DecodeStill(result.Png!);
        if (texture is null)
        {
            return;
        }

        _stillTexture?.Dispose();
        _stillTexture = texture;
        SourceAspect = result.Aspect;
        _liveFrameValid = false;
    }

    private void ApplyOverlayStill(StillFrame result)
    {
        foreach (var track in _overlayTracks)
        {
            if (track.TrackIndex != result.Slot || track.StillToken != result.Token)
            {
                continue;
            }

            var texture = DecodeStill(result.Png!);
            if (texture is null)
            {
                return;
            }

            track.Still?.Dispose();
            track.Still = texture;
            track.Aspect = result.Aspect;
            track.LiveValid = false;
            return;
        }
    }

    private static Texture? DecodeStill(byte[] png)
    {
        try
        {
            using var stream = new MemoryStream(png);
            return new Texture(stream) { Smooth = true };
        }
        catch (Exception)
        {
            return null;
        }
    }

    private static bool IsNormalRate(double rate) => Math.Abs(rate - 1.0) < 0.001;

    private TimeSpan Clamp(TimeSpan value)
    {
        if (value < TimeSpan.Zero)
        {
            return TimeSpan.Zero;
        }

        var duration = Duration;
        return value > duration ? duration : value;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        Stop(keepPosition: false);
        _sound.Dispose();
        _liveTexture.Dispose();
        _stillTexture?.Dispose();

        foreach (var track in _overlayTracks)
        {
            track.Dispose();
        }

        _overlayTracks.Clear();
    }

    private sealed class OverlayTrackState : IDisposable
    {
        public OverlayTrackState(int trackIndex)
        {
            TrackIndex = trackIndex;
        }

        public int TrackIndex { get; }

        public ClipPipeline? Current { get; set; }

        public ClipPipeline? Next { get; set; }

        public Texture? Live { get; private set; }

        public Texture? Still { get; set; }

        public bool LiveValid { get; set; }

        public Guid StillToken { get; set; }

        public double Aspect { get; set; } = 16.0 / 9.0;

        public OverlayTransform Transform { get; set; } = OverlayTransform.Default;

        public Texture? Texture => LiveValid ? Live : Still;

        public Texture EnsureTexture(int width, int height)
        {
            if (Live is { } existing && existing.Size.X == (uint)width && existing.Size.Y == (uint)height)
            {
                return existing;
            }

            Live?.Dispose();
            LiveValid = false;
            Live = new Texture(new Vector2u((uint)width, (uint)height)) { Smooth = true };
            return Live;
        }

        public void ClearFrames()
        {
            LiveValid = false;
            Still?.Dispose();
            Still = null;
        }

        public void DisposePipelines()
        {
            Current?.Dispose();
            Current = null;
            Next?.Dispose();
            Next = null;
        }

        public void Dispose()
        {
            DisposePipelines();
            Live?.Dispose();
            Live = null;
            Still?.Dispose();
            Still = null;
        }
    }
}
