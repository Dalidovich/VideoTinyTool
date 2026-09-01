using VideoTinyTool.Domain;

namespace VideoTinyTool.Commands;

public sealed class DetachAudioCommand : IEditCommand
{
    private readonly Clip _clip;
    private readonly MediaSource? _source;

    private Clip? _detached;
    private Track? _createdTrack;
    private ClipAudio _previousAudio;
    private TimeSpan _followerGap;
    private bool _hasFollower;
    private int _trackIndex = -1;
    private int _index = -1;

    public DetachAudioCommand(Clip clip, MediaSource? source)
    {
        _clip = clip;
        _source = source;
    }

    public string Name => "Detach audio";

    public Clip? Detached => _detached;

    public void Execute(Timeline timeline)
    {
        _index = -1;
        _createdTrack = null;
        _hasFollower = false;

        if (_source is null || !_source.HasAudio)
        {
            return;
        }

        var origin = timeline.TrackIndexOf(_clip);
        if (origin < 0 || timeline.IsAudioTrack(origin))
        {
            return;
        }

        var start = timeline.StartOf(_clip);
        var target = FreeAudioTrack(timeline, start, start + _clip.Duration);

        if (target < 0)
        {
            if (timeline.AddTrack(TrackKind.Audio) is not { } created)
            {
                return;
            }

            _createdTrack = created;
            target = timeline.IndexOfTrack(created);
        }

        var clips = timeline.ClipsOf(target);
        var position = TimeSpan.Zero;
        var index = 0;

        while (index < clips.Count && position + clips[index].LeadingGap + clips[index].Duration <= start)
        {
            position += clips[index].LeadingGap + clips[index].Duration;
            index++;
        }

        _previousAudio = _clip.Audio;
        _trackIndex = target;
        _index = index;
        _detached ??= Clip.Create(_clip.SourceId, _clip.In, _clip.Out);

        timeline.SetClipAudio(_clip, _previousAudio with { Muted = true });
        timeline.SetClipAudio(_detached, _previousAudio);
        timeline.SetLeadingGap(_detached, start - position);
        timeline.Insert(target, index, _detached);

        var updated = timeline.ClipsOf(target);
        if (index + 1 < updated.Count)
        {
            var follower = updated[index + 1];
            _hasFollower = true;
            _followerGap = follower.LeadingGap;
            timeline.SetLeadingGap(follower, follower.LeadingGap - _detached.LeadingGap - _detached.Duration);
        }
    }

    public void Undo(Timeline timeline)
    {
        if (_index < 0)
        {
            return;
        }

        if (_hasFollower)
        {
            timeline.SetLeadingGap(timeline.ClipsOf(_trackIndex)[_index + 1], _followerGap);
        }

        timeline.RemoveAt(_trackIndex, _index);
        timeline.SetClipAudio(_clip, _previousAudio);

        if (_createdTrack is not null)
        {
            timeline.RemoveTrackAt(timeline.IndexOfTrack(_createdTrack));
            _createdTrack = null;
        }

        _index = -1;
        _hasFollower = false;
    }

    private static int FreeAudioTrack(Timeline timeline, TimeSpan start, TimeSpan end)
    {
        for (var index = timeline.FirstAudioTrackIndex; index < timeline.Tracks.Count; index++)
        {
            if (IsFree(timeline.ClipsOf(index), start, end))
            {
                return index;
            }
        }

        return -1;
    }

    private static bool IsFree(IReadOnlyList<Clip> clips, TimeSpan start, TimeSpan end)
    {
        var position = TimeSpan.Zero;

        foreach (var clip in clips)
        {
            position += clip.LeadingGap;

            if (position < end && start < position + clip.Duration)
            {
                return false;
            }

            position += clip.Duration;
        }

        return true;
    }
}
