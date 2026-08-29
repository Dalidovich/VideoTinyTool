using VideoTinyTool.Domain;

namespace VideoTinyTool.Commands;

public sealed class SplitClipCommand : IEditCommand
{
    private readonly Clip _original;
    private readonly Clip _left;
    private readonly Clip _right;
    private int _index;

    public SplitClipCommand(Clip original, TimeSpan sourceSplitPoint)
    {
        _original = original;
        _left = Clip.Create(original.SourceId, original.In, sourceSplitPoint);
        _right = Clip.Create(original.SourceId, sourceSplitPoint, original.Out);
        _index = -1;
    }

    public string Name => "Split clip";

    public Clip Left => _left;

    public Clip Right => _right;

    public void Execute(Timeline timeline)
    {
        _index = timeline.IndexOf(_original);
        if (_index < 0)
        {
            return;
        }

        timeline.RemoveAt(_index);
        timeline.Insert(_index, _right);
        timeline.Insert(_index, _left);
    }

    public void Undo(Timeline timeline)
    {
        var leftIndex = timeline.IndexOf(_left);
        if (leftIndex < 0)
        {
            return;
        }

        timeline.RemoveAt(leftIndex);
        var rightIndex = timeline.IndexOf(_right);
        if (rightIndex >= 0)
        {
            timeline.RemoveAt(rightIndex);
        }

        timeline.Insert(leftIndex, _original);
    }
}
