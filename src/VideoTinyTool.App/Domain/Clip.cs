namespace VideoTinyTool.Domain;

public sealed class Clip
{
    public Clip(Guid id, Guid sourceId, TimeSpan @in, TimeSpan @out)
    {
        if (@out <= @in)
        {
            throw new ArgumentException("Clip Out must be greater than In.", nameof(@out));
        }

        Id = id;
        SourceId = sourceId;
        In = @in;
        Out = @out;
    }

    public static Clip Create(Guid sourceId, TimeSpan @in, TimeSpan @out) => new(Guid.NewGuid(), sourceId, @in, @out);

    public Guid Id { get; }
    public Guid SourceId { get; }
    public TimeSpan In { get; internal set; }
    public TimeSpan Out { get; internal set; }
    public TimeSpan LeadingGap { get; internal set; }
    public OverlayTransform Overlay { get; internal set; } = OverlayTransform.Default;

    public TimeSpan Duration => Out - In;

    public Clip WithBounds(TimeSpan @in, TimeSpan @out) => new(Id, SourceId, @in, @out) { LeadingGap = LeadingGap, Overlay = Overlay };
}
