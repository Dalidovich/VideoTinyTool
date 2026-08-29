using VideoTinyTool.Domain;

namespace VideoTinyTool.Commands;

public sealed class RemoveClipCommand : IEditCommand
{
    private readonly Clip _clip;
    private readonly bool _ripple;
    private int _index;
    private TimeSpan _gap;

    public RemoveClipCommand(Clip clip, bool ripple)
    {
        _clip = clip;
        _ripple = ripple;
        _index = -1;
    }

    public string Name => _ripple ? "Ripple delete clip" : "Remove clip";

    public Clip Clip => _clip;

    public void Execute(Timeline timeline)
    {
        _index = timeline.IndexOf(_clip);
        if (_index < 0)
        {
            return;
        }

        _gap = _clip.LeadingGap;

        if (_index + 1 < timeline.Clips.Count)
        {
            var follower = timeline.Clips[_index + 1];
            timeline.SetLeadingGap(follower, follower.LeadingGap + Displaced());
        }

        timeline.RemoveAt(_index);
    }

    public void Undo(Timeline timeline)
    {
        if (_index < 0)
        {
            return;
        }

        if (_index < timeline.Clips.Count)
        {
            var follower = timeline.Clips[_index];
            timeline.SetLeadingGap(follower, follower.LeadingGap - Displaced());
        }

        timeline.SetLeadingGap(_clip, _gap);
        timeline.Insert(_index, _clip);
    }

    private TimeSpan Displaced() => _ripple ? _gap : _gap + _clip.Duration;
}
