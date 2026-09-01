using VideoTinyTool.Domain;

namespace VideoTinyTool.Commands;

public sealed class SetClipAudioCommand : IEditCommand
{
    private readonly Clip _clip;
    private readonly ClipAudio _audio;
    private ClipAudio _previous;

    public SetClipAudioCommand(Clip clip, ClipAudio audio)
    {
        _clip = clip;
        _audio = audio;
        _previous = clip.Audio;
    }

    public string Name => "Set clip audio";

    public Clip Clip => _clip;

    public void Execute(Timeline timeline)
    {
        _previous = _clip.Audio;
        timeline.SetClipAudio(_clip, _audio);
    }

    public void Undo(Timeline timeline) => timeline.SetClipAudio(_clip, _previous);
}
