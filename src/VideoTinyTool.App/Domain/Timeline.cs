namespace VideoTinyTool.Domain;

public sealed class Timeline
{
    public const int MaxVideoTracks = 4;
    public const int MaxAudioTracks = 4;

    private readonly List<Track> _tracks = new();

    public Timeline()
    {
        var baseTrack = new Track { Owner = this };
        _tracks.Add(baseTrack);
    }

    public IReadOnlyList<Track> Tracks => _tracks;

    public Track BaseTrack => _tracks[0];

    public IReadOnlyList<Clip> Clips => BaseTrack.Clips;

    public event Action? Changed;

    public TimeSpan TotalDuration
    {
        get
        {
            var total = TimeSpan.Zero;
            foreach (var track in _tracks)
            {
                var end = track.Duration;
                if (end > total)
                {
                    total = end;
                }
            }

            return total;
        }
    }

    public int VideoTrackCount
    {
        get
        {
            var count = 0;
            while (count < _tracks.Count && !_tracks[count].IsAudio)
            {
                count++;
            }

            return count;
        }
    }

    public int AudioTrackCount => _tracks.Count - VideoTrackCount;

    public int FirstAudioTrackIndex => VideoTrackCount;

    public bool IsAudioTrack(int index) => index >= 0 && index < _tracks.Count && _tracks[index].IsAudio;

    public IReadOnlyList<Clip> ClipsOf(int trackIndex) => _tracks[trackIndex].Clips;

    public Track? TrackOf(Clip clip)
    {
        foreach (var track in _tracks)
        {
            if (track.Contains(clip))
            {
                return track;
            }
        }

        return null;
    }

    public int TrackIndexOf(Clip clip)
    {
        for (var i = 0; i < _tracks.Count; i++)
        {
            if (_tracks[i].Contains(clip))
            {
                return i;
            }
        }

        return -1;
    }

    public int IndexOfTrack(Track track)
    {
        for (var i = 0; i < _tracks.Count; i++)
        {
            if (ReferenceEquals(_tracks[i], track))
            {
                return i;
            }
        }

        return -1;
    }

    public Track? AddTrack() => AddTrack(TrackKind.Video);

    public Track? AddTrack(TrackKind kind)
    {
        var track = new Track(kind);
        var index = kind == TrackKind.Audio ? _tracks.Count : VideoTrackCount;
        return InsertTrack(index, track) ? track : null;
    }

    public bool InsertTrack(int index, Track track)
    {
        if (index <= 0 || index > _tracks.Count)
        {
            return false;
        }

        var videoCount = VideoTrackCount;

        if (track.IsAudio)
        {
            if (AudioTrackCount >= MaxAudioTracks || index < videoCount)
            {
                return false;
            }
        }
        else if (videoCount >= MaxVideoTracks || index > videoCount)
        {
            return false;
        }

        track.Owner = this;
        _tracks.Insert(index, track);
        Changed?.Invoke();
        return true;
    }

    public bool RemoveTrackAt(int index)
    {
        if (index <= 0 || index >= _tracks.Count)
        {
            return false;
        }

        _tracks[index].Owner = null;
        _tracks.RemoveAt(index);
        Changed?.Invoke();
        return true;
    }

    public void Insert(int index, Clip clip) => Insert(0, index, clip);

    public void Insert(int trackIndex, int index, Clip clip)
    {
        _tracks[trackIndex].Insert(index, clip);
        Changed?.Invoke();
    }

    public void Add(Clip clip) => Insert(0, BaseTrack.Clips.Count, clip);

    public void Add(int trackIndex, Clip clip) => Insert(trackIndex, _tracks[trackIndex].Clips.Count, clip);

    public void RemoveAt(int index) => RemoveAt(0, index);

    public void RemoveAt(int trackIndex, int index)
    {
        _tracks[trackIndex].RemoveAt(index);
        Changed?.Invoke();
    }

    public void Move(int fromIndex, int toIndex) => Move(0, fromIndex, toIndex);

    public void Move(int trackIndex, int fromIndex, int toIndex)
    {
        if (fromIndex == toIndex)
        {
            return;
        }

        _tracks[trackIndex].Move(fromIndex, toIndex);
        Changed?.Invoke();
    }

    public void SetBounds(Clip clip, TimeSpan @in, TimeSpan @out)
    {
        clip.In = @in;
        clip.Out = @out;
        Changed?.Invoke();
    }

    public void SetLeadingGap(Clip clip, TimeSpan gap)
    {
        clip.LeadingGap = gap < TimeSpan.Zero ? TimeSpan.Zero : gap;
        Changed?.Invoke();
    }

    public void SetOverlay(Clip clip, OverlayTransform transform)
    {
        clip.Overlay = transform.Clamped();
        Changed?.Invoke();
    }

    public void SetClipAudio(Clip clip, ClipAudio audio)
    {
        clip.Audio = audio.Clamped();
        Changed?.Invoke();
    }

    public int IndexOf(Clip clip) => BaseTrack.IndexOf(clip);

    public int IndexOf(int trackIndex, Clip clip) => _tracks[trackIndex].IndexOf(clip);

    public Clip? FindById(Guid id)
    {
        foreach (var track in _tracks)
        {
            var clip = track.FindById(id);
            if (clip is not null)
            {
                return clip;
            }
        }

        return null;
    }

    public TimeSpan StartOf(Clip clip) => (TrackOf(clip) ?? BaseTrack).StartOf(clip);

    public TimeSpan StartOf(int index) => BaseTrack.StartOf(index);

    public TimeSpan StartOf(int trackIndex, int index) => _tracks[trackIndex].StartOf(index);

    public TimeSpan NextClipStart(TimeSpan global) => BaseTrack.NextClipStart(global);

    public TimeSpan NextClipStart(int trackIndex, TimeSpan global) => _tracks[trackIndex].NextClipStart(global);

    public TimelineLocation? Resolve(TimeSpan global) => BaseTrack.Resolve(global, 0);

    public TimelineLocation? Resolve(int trackIndex, TimeSpan global) => _tracks[trackIndex].Resolve(global, trackIndex);

    public void RaiseChanged() => Changed?.Invoke();
}
