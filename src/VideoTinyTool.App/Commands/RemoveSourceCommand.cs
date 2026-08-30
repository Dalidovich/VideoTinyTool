using VideoTinyTool.Domain;

namespace VideoTinyTool.Commands;

public sealed class RemoveSourceCommand : IEditCommand
{
    private readonly MediaSource _source;
    private readonly int _sourceIndex;
    private readonly Action<MediaSource> _detach;
    private readonly Action<int, MediaSource> _restore;
    private readonly List<(int TrackIndex, int Index, Clip Clip, TimeSpan Gap)> _removed = new();

    public RemoveSourceCommand(
        MediaSource source,
        int sourceIndex,
        Action<MediaSource> detach,
        Action<int, MediaSource> restore)
    {
        _source = source;
        _sourceIndex = sourceIndex;
        _detach = detach;
        _restore = restore;
    }

    public string Name => "Remove source";

    public MediaSource Source => _source;

    public void Execute(Timeline timeline)
    {
        _removed.Clear();

        for (var trackIndex = 0; trackIndex < timeline.Tracks.Count; trackIndex++)
        {
            var clips = timeline.ClipsOf(trackIndex);
            for (var index = clips.Count - 1; index >= 0; index--)
            {
                var clip = clips[index];
                if (clip.SourceId != _source.Id)
                {
                    continue;
                }

                _removed.Add((trackIndex, index, clip, clip.LeadingGap));

                if (index + 1 < clips.Count)
                {
                    var follower = clips[index + 1];
                    timeline.SetLeadingGap(follower, follower.LeadingGap + clip.LeadingGap + clip.Duration);
                }

                timeline.RemoveAt(trackIndex, index);
            }
        }

        _detach(_source);
    }

    public void Undo(Timeline timeline)
    {
        _restore(_sourceIndex, _source);

        for (var i = _removed.Count - 1; i >= 0; i--)
        {
            var (trackIndex, index, clip, gap) = _removed[i];
            var clips = timeline.ClipsOf(trackIndex);

            if (index < clips.Count)
            {
                var follower = clips[index];
                timeline.SetLeadingGap(follower, follower.LeadingGap - gap - clip.Duration);
            }

            timeline.SetLeadingGap(clip, gap);
            timeline.Insert(trackIndex, index, clip);
        }
    }
}
