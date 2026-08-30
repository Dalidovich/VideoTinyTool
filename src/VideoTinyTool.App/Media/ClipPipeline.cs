using VideoTinyTool.Domain;

namespace VideoTinyTool.Media;

public sealed class ClipPipeline : IDisposable
{
    private const int RingMilliseconds = 1500;

    private readonly VideoFramePipe _video;
    private readonly AudioPcmPipe? _audio;
    private readonly double _frameRate;

    private long _framesTaken;
    private bool _started;

    public ClipPipeline(
        Clip clip,
        MediaSource source,
        int clipIndex,
        TimeSpan clipGlobalStart,
        TimeSpan sourceOffset,
        int previewWidth,
        int previewHeight,
        bool withAudio)
    {
        Clip = clip;
        Source = source;
        ClipIndex = clipIndex;
        GlobalStart = clipGlobalStart;
        SourceOffset = sourceOffset;
        Width = previewWidth;
        Height = previewHeight;
        _frameRate = Math.Clamp(source.FrameRate, 1, 120);

        _video = new VideoFramePipe(source.Path, sourceOffset, previewWidth, previewHeight, _frameRate);

        if (withAudio && source.HasAudio)
        {
            Ring = new PcmRingBuffer(
                AudioPcmPipe.SampleRate * AudioPcmPipe.Channels * AudioPcmPipe.BytesPerSample * RingMilliseconds / 1000);
            _audio = new AudioPcmPipe(source.Path, sourceOffset, Ring);
        }
    }

    public Clip Clip { get; }

    public MediaSource Source { get; }

    public int ClipIndex { get; }

    public TimeSpan GlobalStart { get; }

    public TimeSpan SourceOffset { get; }

    public int Width { get; }

    public int Height { get; }

    public PcmRingBuffer? Ring { get; }

    public TimeSpan GlobalEnd => GlobalStart + (Clip.Out - SourceOffset);

    public bool HasAudio => _audio is not null;

    public int BufferedFrames => _video.BufferedFrames;

    public int BufferedAudioBytes => Ring?.Available ?? 0;

    public bool Primed =>
        BufferedFrames >= 2
        && (!HasAudio || BufferedAudioBytes >= AudioPcmPipe.SampleRate * AudioPcmPipe.Channels * AudioPcmPipe.BytesPerSample / 10);

    public bool Exhausted => _video.Ended;

    public void Start()
    {
        if (_started)
        {
            return;
        }

        _started = true;
        _video.Start();
        _audio?.Start();
    }

    public TimeSpan NextFrameGlobalTime => GlobalStart + TimeSpan.FromSeconds(_framesTaken / _frameRate);

    public bool TryTakeFrame(out byte[] frame)
    {
        if (_video.TryTakeFrame(out frame))
        {
            _framesTaken++;
            return true;
        }

        return false;
    }

    public void Dispose()
    {
        Ring?.Close();
        _audio?.Dispose();
        _video.Dispose();
    }
}
