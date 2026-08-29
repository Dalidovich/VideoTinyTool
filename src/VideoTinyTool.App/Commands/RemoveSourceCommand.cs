using VideoTinyTool.Domain;

namespace VideoTinyTool.Commands;

public sealed class RemoveSourceCommand : IEditCommand
{
    private readonly MediaSource _source;
    private readonly int _sourceIndex;
    private readonly Action<MediaSource> _detach;
    private readonly Action<int, MediaSource> _restore;
    private readonly List<(int Index, Clip Clip, TimeSpan Gap)> _removed = new();

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

        for (var index = timeline.Clips.Count - 1; index >= 0; index--)
        {
            var clip = timeline.Clips[index];
            if (clip.SourceId != _source.Id)
            {
                continue;
            }

            _removed.Add((index, clip, clip.LeadingGap));

            if (index + 1 < timeline.Clips.Count)
            {
                var follower = timeline.Clips[index + 1];
                timeline.SetLeadingGap(follower, follower.LeadingGap + clip.LeadingGap + clip.Duration);
            }

            timeline.RemoveAt(index);
        }

        _detach(_source);
    }

    public void Undo(Timeline timeline)
    {
        _restore(_sourceIndex, _source);

        for (var i = _removed.Count - 1; i >= 0; i--)
        {
            var (index, clip, gap) = _removed[i];

            if (index < timeline.Clips.Count)
            {
                var follower = timeline.Clips[index];
                timeline.SetLeadingGap(follower, follower.LeadingGap - gap - clip.Duration);
            }

            timeline.SetLeadingGap(clip, gap);
            timeline.Insert(index, clip);
        }
    }
}
