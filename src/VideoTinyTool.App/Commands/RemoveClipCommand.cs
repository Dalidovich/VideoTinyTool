using VideoTinyTool.Domain;

namespace VideoTinyTool.Commands;

public sealed class RemoveClipCommand : IEditCommand
{
    private readonly Clip _clip;
    private readonly bool _ripple;
    private int _trackIndex;
    private int _index;
    private TimeSpan _gap;

    public RemoveClipCommand(Clip clip, bool ripple)
    {
        _clip = clip;
        _ripple = ripple;
        _trackIndex = -1;
        _index = -1;
    }

    public string Name => _ripple ? "Ripple delete clip" : "Remove clip";

    public Clip Clip => _clip;

    public void Execute(Timeline timeline)
    {
        _trackIndex = timeline.TrackIndexOf(_clip);
        _index = _trackIndex < 0 ? -1 : timeline.IndexOf(_trackIndex, _clip);
        if (_index < 0)
        {
            return;
        }

        _gap = _clip.LeadingGap;

        var clips = timeline.ClipsOf(_trackIndex);
        if (_index + 1 < clips.Count)
        {
            var follower = clips[_index + 1];
            timeline.SetLeadingGap(follower, follower.LeadingGap + Displaced());
        }

        timeline.RemoveAt(_trackIndex, _index);
    }

    public void Undo(Timeline timeline)
    {
        if (_index < 0)
        {
            return;
        }

        var clips = timeline.ClipsOf(_trackIndex);
        if (_index < clips.Count)
        {
            var follower = clips[_index];
            timeline.SetLeadingGap(follower, follower.LeadingGap - Displaced());
        }

        timeline.SetLeadingGap(_clip, _gap);
        timeline.Insert(_trackIndex, _index, _clip);
    }

    private TimeSpan Displaced() => _ripple ? _gap : _gap + _clip.Duration;
}
