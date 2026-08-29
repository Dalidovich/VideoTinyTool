using VideoTinyTool.Domain;

namespace VideoTinyTool.Commands;

public sealed class CommandHistory
{
    public const int MaxDepth = 100;

    private readonly LinkedList<IEditCommand> _undo = new();
    private readonly Stack<IEditCommand> _redo = new();
    private readonly Timeline _timeline;

    public CommandHistory(Timeline timeline)
    {
        _timeline = timeline;
    }

    public bool CanUndo => _undo.Count > 0;

    public bool CanRedo => _redo.Count > 0;

    public int UndoDepth => _undo.Count;

    public int RedoDepth => _redo.Count;

    public void Execute(IEditCommand command)
    {
        command.Execute(_timeline);
        _undo.AddLast(command);
        _redo.Clear();

        while (_undo.Count > MaxDepth)
        {
            _undo.RemoveFirst();
        }
    }

    public IEditCommand? Undo()
    {
        if (_undo.Last is null)
        {
            return null;
        }

        var command = _undo.Last.Value;
        _undo.RemoveLast();
        command.Undo(_timeline);
        _redo.Push(command);
        return command;
    }

    public IEditCommand? Redo()
    {
        if (_redo.Count == 0)
        {
            return null;
        }

        var command = _redo.Pop();
        command.Execute(_timeline);
        _undo.AddLast(command);
        return command;
    }

    public void Clear()
    {
        _undo.Clear();
        _redo.Clear();
    }
}
