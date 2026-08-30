using VideoTinyTool.Domain;

namespace VideoTinyTool.Commands;

public sealed class SplitClipCommand : IEditCommand
{
    private readonly Clip _original;
    private readonly Clip _left;
    private readonly Clip _right;
    private int _trackIndex;
    private int _index;

    public SplitClipCommand(Clip original, TimeSpan sourceSplitPoint)
    {
        _original = original;
        _left = Clip.Create(original.SourceId, original.In, sourceSplitPoint);
        _right = Clip.Create(original.SourceId, sourceSplitPoint, original.Out);
        _trackIndex = -1;
        _index = -1;
    }

    public string Name => "Split clip";

    public Clip Left => _left;

    public Clip Right => _right;

    public void Execute(Timeline timeline)
    {
        _trackIndex = timeline.TrackIndexOf(_original);
        _index = _trackIndex < 0 ? -1 : timeline.IndexOf(_trackIndex, _original);
        if (_index < 0)
        {
            return;
        }

        timeline.SetOverlay(_left, _original.Overlay);
        timeline.SetOverlay(_right, _original.Overlay);
        timeline.SetLeadingGap(_left, _original.LeadingGap);
        timeline.RemoveAt(_trackIndex, _index);
        timeline.Insert(_trackIndex, _index, _right);
        timeline.Insert(_trackIndex, _index, _left);
    }

    public void Undo(Timeline timeline)
    {
        if (_trackIndex < 0)
        {
            return;
        }

        var leftIndex = timeline.IndexOf(_trackIndex, _left);
        if (leftIndex < 0)
        {
            return;
        }

        timeline.RemoveAt(_trackIndex, leftIndex);
        var rightIndex = timeline.IndexOf(_trackIndex, _right);
        if (rightIndex >= 0)
        {
            timeline.RemoveAt(_trackIndex, rightIndex);
        }

        timeline.Insert(_trackIndex, leftIndex, _original);
    }
}
