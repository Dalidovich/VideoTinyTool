using VideoTinyTool.Domain;

namespace VideoTinyTool.Commands;

public sealed class RemoveTrackCommand : IEditCommand
{
    private readonly int _trackIndex;
    private Track? _track;

    public RemoveTrackCommand(int trackIndex)
    {
        _trackIndex = trackIndex;
    }

    public string Name => "Remove track";

    public Track? Track => _track;

    public void Execute(Timeline timeline)
    {
        if (_trackIndex <= 0 || _trackIndex >= timeline.Tracks.Count)
        {
            _track = null;
            return;
        }

        _track = timeline.Tracks[_trackIndex];
        timeline.RemoveTrackAt(_trackIndex);
    }

    public void Undo(Timeline timeline)
    {
        if (_track is null)
        {
            return;
        }

        timeline.InsertTrack(_trackIndex, _track);
    }
}
