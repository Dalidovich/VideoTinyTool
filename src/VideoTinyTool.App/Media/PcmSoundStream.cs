using SFML.Audio;
using SFML.System;

namespace VideoTinyTool.Media;

public sealed class PcmSoundStream : SoundStream
{
    private const int ChunkMilliseconds = 40;

    private readonly short[] _samples;
    private readonly byte[] _bytes;

    private long _silenceSamples;

    public PcmSoundStream(PcmRingBuffer ring)
    {
        Ring = ring;
        var samplesPerChunk = AudioPcmPipe.SampleRate * AudioPcmPipe.Channels * ChunkMilliseconds / 1000;
        _samples = new short[samplesPerChunk];
        _bytes = new byte[samplesPerChunk * AudioPcmPipe.BytesPerSample];
        Initialize(AudioPcmPipe.Channels, AudioPcmPipe.SampleRate, [SoundChannel.FrontLeft, SoundChannel.FrontRight]);
    }

    public PcmRingBuffer Ring { get; set; }

    public long SilenceSamples => Interlocked.Read(ref _silenceSamples);

    public void ResetSilenceCounter() => Interlocked.Exchange(ref _silenceSamples, 0);

    protected override bool OnGetData(out short[] samples)
    {
        var read = Ring.Read(_bytes);
        if (read < _bytes.Length)
        {
            Array.Clear(_bytes, read, _bytes.Length - read);
            Interlocked.Add(ref _silenceSamples, (_bytes.Length - read) / AudioPcmPipe.BytesPerSample);
        }

        System.Buffer.BlockCopy(_bytes, 0, _samples, 0, _bytes.Length);
        samples = _samples;
        return true;
    }

    protected override void OnSeek(Time timeOffset)
    {
    }
}
