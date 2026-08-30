using VideoTinyTool.Domain;

namespace VideoTinyTool.Commands;

public sealed class ReorderClipCommand : IEditCommand
{
    private readonly Clip _clip;
    private readonly int _fromIndex;
    private readonly int _toIndex;
    private int _trackIndex = -1;

    public ReorderClipCommand(Clip clip, int fromIndex, int toIndex)
    {
        _clip = clip;
        _fromIndex = fromIndex;
        _toIndex = toIndex;
    }

    public string Name => "Reorder clip";

    public Clip Clip => _clip;

    public void Execute(Timeline timeline)
    {
        _trackIndex = timeline.TrackIndexOf(_clip);
        if (_trackIndex < 0)
        {
            return;
        }

        timeline.Move(_trackIndex, _fromIndex, _toIndex);
    }

    public void Undo(Timeline timeline)
    {
        if (_trackIndex < 0)
        {
            return;
        }

        timeline.Move(_trackIndex, _toIndex, _fromIndex);
    }
}
