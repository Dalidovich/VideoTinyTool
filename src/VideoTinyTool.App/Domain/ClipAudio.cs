namespace VideoTinyTool.Domain;

public readonly record struct ClipAudio(float Volume, bool Muted)
{
    public static readonly ClipAudio Default = new(1f, false);

    public float Gain => Muted ? 0f : Math.Clamp(Volume, 0f, 1f);

    public ClipAudio Clamped() => new(Math.Clamp(Volume, 0f, 1f), Muted);
}
