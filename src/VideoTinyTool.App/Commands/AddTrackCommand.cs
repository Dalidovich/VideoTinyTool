using VideoTinyTool.Domain;

namespace VideoTinyTool.Commands;

public sealed class AddTrackCommand : IEditCommand
{
    private Track? _track;
    private int _index = -1;

    public string Name => "Add track";

    public Track? Track => _track;

    public void Execute(Timeline timeline)
    {
        _track ??= new Track();

        if (!timeline.InsertTrack(timeline.Tracks.Count, _track))
        {
            _index = -1;
            return;
        }

        _index = timeline.IndexOfTrack(_track);
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
