using VideoTinyTool.Localization;

namespace VideoTinyTool.Ui;

public readonly record struct ShortcutEntry(string Keys, string Description);

public readonly record struct ShortcutGroup(string Title, IReadOnlyList<ShortcutEntry> Entries);

public static class ShortcutReference
{
    public static IReadOnlyList<ShortcutGroup> LeftColumn =>
    [
        new ShortcutGroup(I18n.Help.Playback,
        [
            new ShortcutEntry("Space", I18n.Help.PlayPause),
            new ShortcutEntry("J / K / L", I18n.Help.Shuttle),
            new ShortcutEntry("Left / Right", I18n.Help.StepFrame),
            new ShortcutEntry("Shift+Left / Right", I18n.Help.StepSecond),
            new ShortcutEntry("Home / End", I18n.Help.JumpToEnds)
        ]),
        new ShortcutGroup(I18n.Help.View,
        [
            new ShortcutEntry("+ / -", I18n.Help.Zoom),
            new ShortcutEntry("Ctrl+Wheel", I18n.Help.ZoomAtCursor),
            new ShortcutEntry("Wheel", I18n.Help.ScrollTimeline),
            new ShortcutEntry("F1", I18n.Help.ShowHelp)
        ])
    ];

    public static IReadOnlyList<ShortcutGroup> RightColumn =>
    [
        new ShortcutGroup(I18n.Help.Editing,
        [
            new ShortcutEntry("S / Ctrl+K", I18n.Help.Split),
            new ShortcutEntry("I / O", I18n.Help.Trim),
            new ShortcutEntry("Delete", I18n.Help.Remove),
            new ShortcutEntry("Shift+Delete", I18n.Help.RippleDelete),
            new ShortcutEntry("Ctrl+Z", I18n.Help.Undo),
            new ShortcutEntry("Ctrl+Shift+Z / Ctrl+Y", I18n.Help.Redo)
        ]),
        new ShortcutGroup(I18n.Help.Files,
        [
            new ShortcutEntry("Ctrl+O", I18n.Help.Import),
            new ShortcutEntry("Ctrl+M", I18n.Help.Export)
        ]),
        new ShortcutGroup(I18n.Help.Dialogs,
        [
            new ShortcutEntry("Enter", I18n.Help.Confirm),
            new ShortcutEntry("Esc", I18n.Help.Dismiss)
        ])
    ];
}
