using VideoTinyTool.Domain;

namespace VideoTinyTool.Commands;

public sealed class TrimClipCommand : IEditCommand
{
    private readonly Clip _clip;
    private readonly TimeSpan _newIn;
    private readonly TimeSpan _newOut;
    private TimeSpan _oldIn;
    private TimeSpan _oldOut;

    public TrimClipCommand(Clip clip, TimeSpan newIn, TimeSpan newOut)
    {
        _clip = clip;
        _newIn = newIn;
        _newOut = newOut;
        _oldIn = clip.In;
        _oldOut = clip.Out;
    }

    public string Name => "Trim clip";

    public Clip Clip => _clip;

    public void Execute(Timeline timeline)
    {
        _oldIn = _clip.In;
        _oldOut = _clip.Out;
        timeline.SetBounds(_clip, _newIn, _newOut);
    }

    public void Undo(Timeline timeline) => timeline.SetBounds(_clip, _oldIn, _oldOut);
}
