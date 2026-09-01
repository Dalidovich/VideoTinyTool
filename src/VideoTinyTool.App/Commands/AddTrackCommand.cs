using VideoTinyTool.Domain;

namespace VideoTinyTool.Commands;

public sealed class AddTrackCommand : IEditCommand
{
    private readonly TrackKind _kind;
    private Track? _track;
    private int _index = -1;

    public AddTrackCommand()
        : this(TrackKind.Video)
    {
    }

    public AddTrackCommand(TrackKind kind)
    {
        _kind = kind;
    }

    public string Name => "Add track";

    public Track? Track => _track;

    public void Execute(Timeline timeline)
    {
        _track ??= new Track(_kind);

        var index = _kind == TrackKind.Audio ? timeline.Tracks.Count : timeline.VideoTrackCount;
        _index = timeline.InsertTrack(index, _track) ? timeline.IndexOfTrack(_track) : -1;
    }

    public void Undo(Timeline timeline)
    {
        if (_index < 0)
        {
            return;
        }

        timeline.RemoveTrackAt(_index);
        _index = -1;
    }
}
