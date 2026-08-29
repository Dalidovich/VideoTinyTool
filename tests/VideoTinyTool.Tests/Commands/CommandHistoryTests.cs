using VideoTinyTool.Commands;
using VideoTinyTool.Domain;

namespace VideoTinyTool.Tests.Commands;

public class CommandHistoryTests
{
    private static AddClipCommand Add(Timeline timeline, Guid sourceId) =>
        new(TestData.Clip(sourceId, 0, 1), timeline.Clips.Count);

    [Fact]
    public void NewHistory_CanNeitherUndoNorRedo()
    {
        var history = new CommandHistory(new Timeline());

        Assert.False(history.CanUndo);
        Assert.False(history.CanRedo);
        Assert.Null(history.Undo());
        Assert.Null(history.Redo());
    }

    [Fact]
    public void UndoThenRedo_ReturnsTheTimelineToTheExecutedState()
    {
        var timeline = new Timeline();
        var history = new CommandHistory(timeline);
        var sourceId = Guid.NewGuid();

        history.Execute(Add(timeline, sourceId));
        history.Execute(Add(timeline, sourceId));
        Assert.Equal(2, timeline.Clips.Count);

        history.Undo();
        Assert.Single(timeline.Clips);

        history.Redo();
        Assert.Equal(2, timeline.Clips.Count);
    }

    [Fact]
    public void ExecutingAfterUndo_ClearsTheRedoStack()
    {
        var timeline = new Timeline();
        var history = new CommandHistory(timeline);
        var sourceId = Guid.NewGuid();

        history.Execute(Add(timeline, sourceId));
        history.Undo();
        Assert.True(history.CanRedo);

        history.Execute(Add(timeline, sourceId));

        Assert.False(history.CanRedo);
        Assert.Equal(1, history.UndoDepth);
    }

    [Fact]
    public void History_EvictsTheOldestCommandBeyondTheDepthLimit()
    {
        var timeline = new Timeline();
        var history = new CommandHistory(timeline);
        var sourceId = Guid.NewGuid();

        for (var i = 0; i < CommandHistory.MaxDepth + 25; i++)
        {
            history.Execute(Add(timeline, sourceId));
        }

        Assert.Equal(CommandHistory.MaxDepth, history.UndoDepth);
        Assert.Equal(CommandHistory.MaxDepth + 25, timeline.Clips.Count);

        for (var i = 0; i < CommandHistory.MaxDepth; i++)
        {
            history.Undo();
        }

        Assert.False(history.CanUndo);
        Assert.Equal(25, timeline.Clips.Count);
    }
}
