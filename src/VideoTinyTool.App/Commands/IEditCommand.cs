using VideoTinyTool.Domain;

namespace VideoTinyTool.Commands;

public interface IEditCommand
{
    string Name { get; }

    void Execute(Timeline timeline);

    void Undo(Timeline timeline);
}
