using VideoTinyTool.Domain;

namespace VideoTinyTool.Commands;

public sealed class AddClipCommand : IEditCommand
{
    private readonly Clip _clip;
    private readonly int _index;
    private readonly int _fixedTrackIndex;
    private readonly TrackKind? _kind;

    private int _trackIndex;
    private bool _createdTrack;

    public AddClipCommand(Clip clip, int index, int trackIndex = 0)
    {
        _clip = clip;
        _index = index;
        _fixedTrackIndex = trackIndex;
        _trackIndex = trackIndex;
    }

    public AddClipCommand(Clip clip, TrackKind kind)
    {
        _clip = clip;
        _index = -1;
        _fixedTrackIndex = -1;
        _trackIndex = -1;
        _kind = kind;
    }

    public string Name => "Add clip";

    public Clip Clip => _clip;

    public int TrackIndex => _trackIndex;

    public void Execute(Timeline timeline)
    {
        if (_kind is null)
        {
            _trackIndex = _fixedTrackIndex;
            timeline.Insert(_trackIndex, _index, _clip);
            return;
        }

        var target = LastTrackIndexOf(timeline, _kind.Value);
        _createdTrack = target < 0;

        if (_createdTrack)
        {
            if (timeline.AddTrack(_kind.Value) is null)
            {
                _createdTrack = false;
                return;
            }

            target = LastTrackIndexOf(timeline, _kind.Value);
        }

        _trackIndex = target;
        timeline.Add(_trackIndex, _clip);
    }

    public void Undo(Timeline timeline)
    {
        if (_trackIndex < 0)
        {
            return;
        }

        var index = timeline.IndexOf(_trackIndex, _clip);
        if (index >= 0)
        {
            timeline.RemoveAt(_trackIndex, index);
        }

        if (_createdTrack)
        {
            timeline.RemoveTrackAt(_trackIndex);
            _createdTrack = false;
        }
    }

    private static int LastTrackIndexOf(Timeline timeline, TrackKind kind)
    {
        for (var i = timeline.Tracks.Count - 1; i >= 0; i--)
        {
            if (timeline.Tracks[i].Kind == kind)
            {
                return i;
            }
        }

        return -1;
    }
}
