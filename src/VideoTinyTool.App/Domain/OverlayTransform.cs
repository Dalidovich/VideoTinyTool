namespace VideoTinyTool.Domain;

public readonly record struct OverlayTransform(float X, float Y, float Width, float Opacity)
{
    public const float MinWidth = 0.05f;

    public static readonly OverlayTransform Default = new(0.62f, 0.06f, 0.32f, 1f);

    public OverlayTransform Clamped() => new(
        Math.Clamp(X, 0f, 1f),
        Math.Clamp(Y, 0f, 1f),
        Math.Clamp(Width, MinWidth, 1f),
        Math.Clamp(Opacity, 0f, 1f));
}
