namespace VideoTinyTool.Domain;

public sealed class Track
{
    private readonly List<Clip> _clips = new();

    public Track()
        : this(Guid.NewGuid())
    {
    }

    public Track(Guid id)
    {
        Id = id;
    }

    public Guid Id { get; }

    public IReadOnlyList<Clip> Clips => _clips;

    internal Timeline? Owner { get; set; }

    public bool IsBase => Owner is { } owner && owner.Tracks.Count > 0 && ReferenceEquals(owner.Tracks[0], this);

    public TimeSpan Duration
    {
        get
        {
            var total = TimeSpan.Zero;
            foreach (var clip in _clips)
            {
                total += clip.LeadingGap + clip.Duration;
            }

            return total;
        }
    }

    public int IndexOf(Clip clip) => _clips.IndexOf(clip);

    public bool Contains(Clip clip) => IndexOf(clip) >= 0;

    public Clip? FindById(Guid id)
    {
        foreach (var clip in _clips)
        {
            if (clip.Id == id)
            {
                return clip;
            }
        }

        return null;
    }

    public TimeSpan StartOf(Clip clip)
    {
        var start = TimeSpan.Zero;
        foreach (var current in _clips)
        {
            start += current.LeadingGap;
            if (ReferenceEquals(current, clip))
            {
                return start;
            }

            start += current.Duration;
        }

        return TimeSpan.Zero;
    }

    public TimeSpan StartOf(int index)
    {
        var start = TimeSpan.Zero;
        for (var i = 0; i < _clips.Count; i++)
        {
            start += _clips[i].LeadingGap;
            if (i == index)
            {
                return start;
            }

            start += _clips[i].Duration;
        }

        return start;
    }

    public TimeSpan NextClipStart(TimeSpan global)
    {
        var start = TimeSpan.Zero;
        foreach (var clip in _clips)
        {
            start += clip.LeadingGap;
            if (start >= global)
            {
                return start;
            }

            start += clip.Duration;
        }

        return start;
    }

    public TimelineLocation? Resolve(TimeSpan global, int trackIndex)
    {
        if (global < TimeSpan.Zero)
        {
            return null;
        }

        var start = TimeSpan.Zero;
        for (var i = 0; i < _clips.Count; i++)
        {
            var clip = _clips[i];
            start += clip.LeadingGap;

            if (global < start)
            {
                return null;
            }

            var end = start + clip.Duration;
            if (global < end)
            {
                return new TimelineLocation(clip, i, clip.In + (global - start), trackIndex);
            }

            start = end;
        }

        return null;
    }

    internal void Insert(int index, Clip clip) => _clips.Insert(Math.Clamp(index, 0, _clips.Count), clip);

    internal void RemoveAt(int index) => _clips.RemoveAt(index);

    internal void Move(int fromIndex, int toIndex)
    {
        var clip = _clips[fromIndex];
        _clips.RemoveAt(fromIndex);
        _clips.Insert(Math.Clamp(toIndex, 0, _clips.Count), clip);
    }
}
