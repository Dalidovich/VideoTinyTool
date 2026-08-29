using VideoTinyTool.Domain;

namespace VideoTinyTool.Commands;

public sealed class RemoveClipCommand : IEditCommand
{
    private readonly Clip _clip;
    private int _index;

    public RemoveClipCommand(Clip clip)
    {
        _clip = clip;
        _index = -1;
    }

    public string Name => "Remove clip";

    public Clip Clip => _clip;

    public void Execute(Timeline timeline)
    {
        _index = timeline.IndexOf(_clip);
        if (_index >= 0)
        {
            timeline.RemoveAt(_index);
        }
    }

    public void Undo(Timeline timeline)
    {
        if (_index >= 0)
        {
            timeline.Insert(_index, _clip);
        }
    }
}
