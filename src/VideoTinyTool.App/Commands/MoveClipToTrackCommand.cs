using VideoTinyTool.Domain;

namespace VideoTinyTool.Commands;

public sealed class MoveClipToTrackCommand : IEditCommand
{
    private readonly Clip _clip;
    private readonly int _targetTrackIndex;
    private readonly TimeSpan _targetStart;
    private int _sourceTrackIndex = -1;
    private int _sourceIndex = -1;
    private TimeSpan _sourceGap;
    private TimeSpan? _sourceFollowerGap;
    private TimeSpan? _targetFollowerGap;

    public MoveClipToTrackCommand(Clip clip, int targetTrackIndex, TimeSpan targetStart)
    {
        _clip = clip;
        _targetTrackIndex = targetTrackIndex;
        _targetStart = targetStart < TimeSpan.Zero ? TimeSpan.Zero : targetStart;
    }

    public string Name => "Move clip to track";

    public Clip Clip => _clip;

    public void Execute(Timeline timeline)
    {
        _sourceTrackIndex = timeline.TrackIndexOf(_clip);
        if (_sourceTrackIndex < 0 || _targetTrackIndex < 0 || _targetTrackIndex >= timeline.Tracks.Count)
        {
            _sourceTrackIndex = -1;
            return;
        }

        _sourceIndex = timeline.IndexOf(_sourceTrackIndex, _clip);
        _sourceGap = _clip.LeadingGap;
        _sourceFollowerGap = null;
        _targetFollowerGap = null;

        var source = timeline.ClipsOf(_sourceTrackIndex);
        if (_sourceIndex + 1 < source.Count)
        {
            var follower = source[_sourceIndex + 1];
            _sourceFollowerGap = follower.LeadingGap;
            timeline.SetLeadingGap(follower, follower.LeadingGap + _sourceGap + _clip.Duration);
        }

        timeline.RemoveAt(_sourceTrackIndex, _sourceIndex);

        var target = timeline.ClipsOf(_targetTrackIndex);
        var cursor = TimeSpan.Zero;
        var insertIndex = target.Count;

        for (var i = 0; i < target.Count; i++)
        {
            var start = cursor + target[i].LeadingGap;
            if (start >= _targetStart)
            {
                insertIndex = i;
                break;
            }

            cursor = start + target[i].Duration;
        }

        var gap = _targetStart - cursor;
        if (gap < TimeSpan.Zero)
        {
            gap = TimeSpan.Zero;
        }

        if (insertIndex < target.Count)
        {
            var follower = target[insertIndex];
            _targetFollowerGap = follower.LeadingGap;
            timeline.SetLeadingGap(follower, follower.LeadingGap - gap - _clip.Duration);
        }

        timeline.SetLeadingGap(_clip, gap);
        timeline.Insert(_targetTrackIndex, insertIndex, _clip);
    }

    public void Undo(Timeline timeline)
    {
        if (_sourceTrackIndex < 0)
        {
            return;
        }

        var index = timeline.IndexOf(_targetTrackIndex, _clip);
        if (index >= 0)
        {
            timeline.RemoveAt(_targetTrackIndex, index);

            if (_targetFollowerGap is { } targetGap && index < timeline.ClipsOf(_targetTrackIndex).Count)
            {
                timeline.SetLeadingGap(timeline.ClipsOf(_targetTrackIndex)[index], targetGap);
            }
        }

        if (_sourceFollowerGap is { } sourceGap && _sourceIndex < timeline.ClipsOf(_sourceTrackIndex).Count)
        {
            timeline.SetLeadingGap(timeline.ClipsOf(_sourceTrackIndex)[_sourceIndex], sourceGap);
        }

        timeline.SetLeadingGap(_clip, _sourceGap);
        timeline.Insert(_sourceTrackIndex, _sourceIndex, _clip);
    }
}
