using VideoTinyTool.Domain;

namespace VideoTinyTool.Commands;

public sealed class AddClipCommand : IEditCommand
{
    private readonly Clip _clip;
    private readonly int _index;
    private readonly int _trackIndex;

    public AddClipCommand(Clip clip, int index, int trackIndex = 0)
    {
        _clip = clip;
        _index = index;
        _trackIndex = trackIndex;
    }

    public string Name => "Add clip";

    public Clip Clip => _clip;

    public int TrackIndex => _trackIndex;

    public void Execute(Timeline timeline) => timeline.Insert(_trackIndex, _index, _clip);

    public void Undo(Timeline timeline)
    {
        var index = timeline.IndexOf(_trackIndex, _clip);
        if (index >= 0)
        {
            timeline.RemoveAt(_trackIndex, index);
        }
    }
}
