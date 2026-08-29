namespace VideoTinyTool.Domain;

public sealed class Timeline
{
    private readonly List<Clip> _clips = new();

    public IReadOnlyList<Clip> Clips => _clips;

    public event Action? Changed;

    public TimeSpan TotalDuration
    {
        get
        {
            var total = TimeSpan.Zero;
            foreach (var clip in _clips)
            {
                total += clip.Duration;
            }

            return total;
        }
    }

    public void Insert(int index, Clip clip)
    {
        _clips.Insert(Math.Clamp(index, 0, _clips.Count), clip);
        Changed?.Invoke();
    }

    public void Add(Clip clip) => Insert(_clips.Count, clip);

    public void RemoveAt(int index)
    {
        _clips.RemoveAt(index);
        Changed?.Invoke();
    }

    public void Move(int fromIndex, int toIndex)
    {
        if (fromIndex == toIndex)
        {
            return;
        }

        var clip = _clips[fromIndex];
        _clips.RemoveAt(fromIndex);
        _clips.Insert(Math.Clamp(toIndex, 0, _clips.Count), clip);
        Changed?.Invoke();
    }

    public void SetBounds(Clip clip, TimeSpan @in, TimeSpan @out)
    {
        clip.In = @in;
        clip.Out = @out;
        Changed?.Invoke();
    }

    public int IndexOf(Clip clip) => _clips.IndexOf(clip);

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
            if (ReferenceEquals(current, clip))
            {
                return start;
            }

            start += current.Duration;
        }

        return TimeSpan.Zero;
    }

    public TimelineLocation? Resolve(TimeSpan global)
    {
        if (global < TimeSpan.Zero || global >= TotalDuration)
        {
            return null;
        }

        var start = TimeSpan.Zero;
        for (var i = 0; i < _clips.Count; i++)
        {
            var clip = _clips[i];
            var end = start + clip.Duration;
            if (global < end)
            {
                return new TimelineLocation(clip, i, clip.In + (global - start));
            }

            start = end;
        }

        return null;
    }

    public void RaiseChanged() => Changed?.Invoke();
}
