using VideoTinyTool.Application;
using VideoTinyTool.Commands;
using VideoTinyTool.Domain;
using VideoTinyTool.Media;

namespace VideoTinyTool.Ui;

public interface IEditorHost
{
    Timeline Timeline { get; }

    IReadOnlyList<MediaSource> Sources { get; }

    MediaSource? SelectedSource { get; }

    Clip? SelectedClip { get; }

    PreviewPlayer Player { get; }

    ThumbnailService Thumbnails { get; }

    CommandHistory History { get; }

    AppSettings Settings { get; }

    MediaSource? FindSource(Guid id);

    bool SourceMissing(Guid sourceId);

    void SelectSource(MediaSource? source);

    void SelectClip(Clip? clip);

    void AppendSourceToTimeline(MediaSource source);

    void RemoveSource(MediaSource source);

    void Execute(IEditCommand command);

    void SeekTo(TimeSpan position, bool scrubbing);

    void EndScrub(bool resumePlayback);

    void ImportFiles();

    void ExportTimeline();

    void SplitAtPlayhead();

    void RemoveSelectedClip();

    void TrimSelectedToPlayhead(bool trimIn);

    void Undo();

    void Redo();
}
