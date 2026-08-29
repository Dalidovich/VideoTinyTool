using SFML.Window;

namespace VideoTinyTool.Ui;

public enum EditorCommand
{
    None,
    TogglePlay,
    ShuttleBackward,
    ShuttleStop,
    ShuttleForward,
    StepFrameBack,
    StepFrameForward,
    StepSecondBack,
    StepSecondForward,
    GoToStart,
    GoToEnd,
    TrimIn,
    TrimOut,
    Split,
    Delete,
    RippleDelete,
    Undo,
    Redo,
    Import,
    Export,
    ZoomIn,
    ZoomOut
}

public static class Shortcuts
{
    public static EditorCommand Resolve(KeyEventArgs key)
    {
        if (key.Control)
        {
            return key.Code switch
            {
                Keyboard.Key.Z => key.Shift ? EditorCommand.Redo : EditorCommand.Undo,
                Keyboard.Key.Y => EditorCommand.Redo,
                Keyboard.Key.K => EditorCommand.Split,
                Keyboard.Key.O => EditorCommand.Import,
                Keyboard.Key.M => EditorCommand.Export,
                _ => EditorCommand.None
            };
        }

        if (key.Shift)
        {
            return key.Code switch
            {
                Keyboard.Key.Left => EditorCommand.StepSecondBack,
                Keyboard.Key.Right => EditorCommand.StepSecondForward,
                Keyboard.Key.Delete => EditorCommand.RippleDelete,
                Keyboard.Key.Equal => EditorCommand.ZoomIn,
                _ => EditorCommand.None
            };
        }

        return key.Code switch
        {
            Keyboard.Key.Space => EditorCommand.TogglePlay,
            Keyboard.Key.J => EditorCommand.ShuttleBackward,
            Keyboard.Key.K => EditorCommand.ShuttleStop,
            Keyboard.Key.L => EditorCommand.ShuttleForward,
            Keyboard.Key.Left => EditorCommand.StepFrameBack,
            Keyboard.Key.Right => EditorCommand.StepFrameForward,
            Keyboard.Key.Home => EditorCommand.GoToStart,
            Keyboard.Key.End => EditorCommand.GoToEnd,
            Keyboard.Key.I => EditorCommand.TrimIn,
            Keyboard.Key.O => EditorCommand.TrimOut,
            Keyboard.Key.S => EditorCommand.Split,
            Keyboard.Key.Delete => EditorCommand.Delete,
            Keyboard.Key.Add or Keyboard.Key.Equal => EditorCommand.ZoomIn,
            Keyboard.Key.Subtract or Keyboard.Key.Hyphen => EditorCommand.ZoomOut,
            _ => EditorCommand.None
        };
    }
}
