using VideoTinyTool.Domain;

namespace VideoTinyTool.Commands;

public sealed class SetOverlayTransformCommand : IEditCommand
{
    private readonly Clip _clip;
    private readonly OverlayTransform _transform;
    private OverlayTransform _previous;

    public SetOverlayTransformCommand(Clip clip, OverlayTransform transform)
    {
        _clip = clip;
        _transform = transform;
        _previous = clip.Overlay;
    }

    public string Name => "Set overlay transform";

    public Clip Clip => _clip;

    public void Execute(Timeline timeline)
    {
        _previous = _clip.Overlay;
        timeline.SetOverlay(_clip, _transform);
    }

    public void Undo(Timeline timeline) => timeline.SetOverlay(_clip, _previous);
}
