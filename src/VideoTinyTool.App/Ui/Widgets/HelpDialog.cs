using SFML.System;
using VideoTinyTool.Localization;

namespace VideoTinyTool.Ui.Widgets;

public sealed class HelpDialog : ModalDialog
{
    private const float RowHeight = 20f;
    private const float HeaderHeight = 25f;
    private const float GroupGap = 14f;
    private const float ColumnGap = 36f;
    private const float KeyGap = 18f;
    private const uint KeySize = Theme.FontSizeSmall;
    private const uint TextSize = Theme.FontSizeSmall;

    private bool _pressedOutside;

    public HelpDialog() : base(I18n.Help.Title, string.Empty)
    {
        AddButton(I18n.Dialogs.Ok, ButtonStyle.Accent, Close);
    }

    private static IEnumerable<ShortcutGroup> AllGroups =>
        ShortcutReference.LeftColumn.Concat(ShortcutReference.RightColumn);

    public override void OnMouseDown(Vector2f point)
    {
        _pressedOutside = !Bounds.Contains(point);
        base.OnMouseDown(point);
    }

    public override void OnMouseUp(Vector2f point)
    {
        base.OnMouseUp(point);

        if (_pressedOutside && !Bounds.Contains(point))
        {
            Close();
        }

        _pressedOutside = false;
    }

    public override float ContentHeight(Renderer renderer, float contentWidth) =>
        Math.Max(ColumnHeight(ShortcutReference.LeftColumn), ColumnHeight(ShortcutReference.RightColumn));

    protected override float MeasureWidth(Renderer renderer)
    {
        var column = KeyColumnWidth(renderer) + KeyGap + DescriptionWidth(renderer);
        return Math.Max(
            base.MeasureWidth(renderer),
            MathF.Round((column * 2) + ColumnGap + (HorizontalPadding * 2)));
    }

    protected override void DrawContent(Renderer renderer, float left, float top, float width)
    {
        var columnWidth = (width - ColumnGap) / 2f;
        var keyWidth = KeyColumnWidth(renderer);

        DrawColumn(renderer, ShortcutReference.LeftColumn, left, top, columnWidth, keyWidth);
        DrawColumn(renderer, ShortcutReference.RightColumn, left + columnWidth + ColumnGap, top, columnWidth, keyWidth);
    }

    private static void DrawColumn(
        Renderer renderer,
        IReadOnlyList<ShortcutGroup> groups,
        float left,
        float top,
        float width,
        float keyWidth)
    {
        foreach (var group in groups)
        {
            renderer.DrawText(group.Title, left, top, Theme.FontSizeLabel, Theme.Accent, TextFont.SemiBold);
            renderer.HorizontalLine(left, MathF.Round(top + 17f), width, Theme.Line);
            top += HeaderHeight;

            foreach (var entry in group.Entries)
            {
                renderer.DrawText(entry.Keys, left, top, KeySize, Theme.Text, TextFont.Mono);
                renderer.DrawText(
                    renderer.Ellipsize(entry.Description, width - keyWidth - KeyGap, TextSize),
                    left + keyWidth + KeyGap,
                    top,
                    TextSize,
                    Theme.TextDim);
                top += RowHeight;
            }

            top += GroupGap;
        }
    }

    private static float ColumnHeight(IReadOnlyList<ShortcutGroup> groups) =>
        groups.Sum(group => HeaderHeight + (group.Entries.Count * RowHeight) + GroupGap) - GroupGap;

    private static float KeyColumnWidth(Renderer renderer) =>
        AllGroups
            .SelectMany(group => group.Entries)
            .Max(entry => MathF.Ceiling(renderer.MeasureText(entry.Keys, KeySize, TextFont.Mono)));

    private static float DescriptionWidth(Renderer renderer) =>
        AllGroups
            .SelectMany(group => group.Entries)
            .Max(entry => MathF.Ceiling(renderer.MeasureText(entry.Description, TextSize)));
}
