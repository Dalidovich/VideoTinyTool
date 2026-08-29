using SFML.Graphics;
using SFML.System;
using VideoTinyTool.Domain;

namespace VideoTinyTool.Media;

public sealed class PreviewPlayer : IDisposable
{
    private static readonly TimeSpan PrefetchLead = TimeSpan.FromMilliseconds(500);
    private static readonly TimeSpan ResumeDelay = TimeSpan.FromMilliseconds(150);
    private static readonly TimeSpan PrimeTimeout = TimeSpan.FromMilliseconds(600);

    private readonly Timeline _timeline;
    private readonly Func<Guid, MediaSource?> _sourceLookup;
    private readonly StillFrameService _stillFrames;
    private readonly PlaybackClock _clock = new();
    private readonly PcmRingBuffer _silentRing;
    private readonly PcmSoundStream _sound;
    private readonly int _previewWidth;
    private readonly int _previewHeight;
    private readonly Texture _liveTexture;

    private Texture? _stillTexture;
    private ClipPipeline? _current;
    private ClipPipeline? _next;

    private TimeSpan _pausedPosition = TimeSpan.Zero;
    private TimeSpan _audioAnchor = TimeSpan.Zero;
    private TimeSpan _globalAnchor = TimeSpan.Zero;
    private DateTime? _resumeAt;
    private Guid _stillToken;
    private double _rate = 1.0;
    private bool _scrubbing;
    private bool _liveFrameValid;
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
    }

    public bool IsPlaying { get; private set; }

    public double Rate => _rate;

    public double SourceAspect { get; private set; } = 16.0 / 9.0;

    public Texture? CurrentTexture => _liveFrameValid ? _liveTexture : _stillTexture;

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
        _pausedPosition = position;

        if (_timeline.Clips.Count == 0)
        {
            _liveFrameValid = false;
            _stillTexture?.Dispose();
            _stillTexture = null;
            return;
        }

        RequestStillFrame(position);
        _resumeAt = wasPlaying ? DateTime.UtcNow + ResumeDelay : null;
    }

    public void Update()
    {
        PumpStillFrame();

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

        pipeline.Dispose();
        _current = _next;
        _next = null;

        _current ??= CreatePipeline(pipeline.ClipIndex + 1, pipeline.GlobalEnd, IsNormalRate(_rate));
        _current?.Start();

        _sound.Ring = _current?.Ring ?? _silentRing;
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
            _pausedPosition = target;
            return;
        }

        var source = _sourceLookup(location.Value.Clip.SourceId);
        if (source is null)
        {
            _pausedPosition = target;
            return;
        }

        DisposePipelines();

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
            _sound.Ring = _current.Ring ?? _silentRing;
            _sound.Play();
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

    private void Stop(bool keepPosition)
    {
        if (IsPlaying && keepPosition)
        {
            _pausedPosition = Clamp(Position);
        }

        IsPlaying = false;
        _resumeAt = null;
        _clock.Reset(_pausedPosition);

        try
        {
            _sound.Stop();
        }
        catch (Exception)
        {
            // The audio device may already be gone.
        }

        _sound.Ring = _silentRing;
        _silentRing.Clear();
        DisposePipelines();
    }

    private void DisposePipelines()
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
            return;
        }

        var probe = position >= Duration ? Duration - TimeSpan.FromMilliseconds(40) : position;
        if (probe < TimeSpan.Zero)
        {
            probe = TimeSpan.Zero;
        }

        var location = _timeline.Resolve(probe);
        if (location is null)
        {
            return;
        }

        var source = _sourceLookup(location.Value.Clip.SourceId);
        if (source is null)
        {
            return;
        }

        _stillToken = _stillFrames.Request(source, location.Value.SourceOffset, _previewHeight);
        SourceAspect = source.AspectRatio;
    }

    private void PumpStillFrame()
    {
        var result = _stillFrames.TakeResult();
        if (result is null || result.Token != _stillToken || result.Png is not { Length: > 0 })
        {
            return;
        }

        try
        {
            using var stream = new MemoryStream(result.Png);
            var texture = new Texture(stream) { Smooth = true };
            _stillTexture?.Dispose();
            _stillTexture = texture;
            SourceAspect = result.Aspect;
            _liveFrameValid = false;
        }
        catch (Exception)
        {
            // A frame that will not decode is simply skipped.
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
    }
}
