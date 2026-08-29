using VideoTinyTool.Domain;

namespace VideoTinyTool.Commands;

public sealed class AddClipCommand : IEditCommand
{
    private readonly Clip _clip;
    private readonly int _index;

    public AddClipCommand(Clip clip, int index)
    {
        _clip = clip;
        _index = index;
    }

    public string Name => "Add clip";

    public Clip Clip => _clip;

    public void Execute(Timeline timeline) => timeline.Insert(_index, _clip);

    public void Undo(Timeline timeline)
    {
        var index = timeline.IndexOf(_clip);
        if (index >= 0)
        {
            timeline.RemoveAt(index);
        }
    }
}
