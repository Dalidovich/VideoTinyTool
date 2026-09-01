using SFML.Graphics;
using SFML.System;
using SFML.Window;

namespace VideoTinyTool.Ui.Widgets;

public sealed class ContextMenu
{
    private const float ItemHeight = 24f;
    private const float SeparatorHeight = 7f;
    private const float PaddingX = 12f;
    private const float PaddingY = 5f;
    private const float ShortcutGap = 30f;
    private const float MinWidth = 150f;
    private const float MaxWidth = 420f;

    private readonly record struct Item(string Label, string Shortcut, bool Enabled, Action? Invoke)
    {
        public bool IsSeparator => Invoke is null;
    }

    private readonly List<Item> _items = new();
    private readonly Vector2f _anchor;

    private int _hovered = -1;

    public ContextMenu(Vector2f anchor)
    {
        _anchor = anchor;
    }

    public bool Closed { get; private set; }

    public bool IsEmpty => !_items.Any(item => !item.IsSeparator);

    public FloatRect Bounds { get; private set; }

    public ContextMenu Add(string label, string shortcut, bool enabled, Action invoke)
    {
        _items.Add(new Item(label, shortcut, enabled, invoke));
        return this;
    }

    public ContextMenu Separator()
    {
        if (_items.Count > 0 && !_items[^1].IsSeparator)
        {
            _items.Add(new Item(string.Empty, string.Empty, false, null));
        }

        return this;
    }

    public void Close() => Closed = true;

    public void Layout(Renderer renderer, uint windowWidth, uint windowHeight)
    {
        while (_items.Count > 0 && _items[^1].IsSeparator)
        {
            _items.RemoveAt(_items.Count - 1);
        }

        var width = MinWidth;
        var height = PaddingY * 2;

        foreach (var item in _items)
        {
            if (item.IsSeparator)
            {
                height += SeparatorHeight;
                continue;
            }

            height += ItemHeight;
            width = Math.Max(width, (PaddingX * 2)
                                    + renderer.MeasureText(item.Label, Theme.FontSizeSmall)
                                    + (item.Shortcut.Length == 0
                                        ? 0
                                        : ShortcutGap + renderer.MeasureText(
                                            item.Shortcut,
                                            Theme.FontSizeLabel,
                                            TextFont.Mono)));
        }

        width = MathF.Round(Math.Min(width, MaxWidth));
        height = MathF.Round(height);

        Bounds = new FloatRect(
            new Vector2f(
                MathF.Round(ContextMenuGeometry.Place(_anchor.X, width, windowWidth)),
                MathF.Round(ContextMenuGeometry.Place(_anchor.Y, height, windowHeight))),
            new Vector2f(width, height));
    }

    public void UpdateHover(Vector2f point) => _hovered = IndexAt(point);

    public void OnMouseDown(Vector2f point, Mouse.Button button)
    {
        if (!Bounds.Contains(point))
        {
            Close();
            return;
        }

        if (button != Mouse.Button.Left)
        {
            return;
        }

        var index = IndexAt(point);
        if (index < 0)
        {
            return;
        }

        var invoke = _items[index].Invoke;
        Close();
        invoke?.Invoke();
    }

    public void Draw(Renderer renderer)
    {
        renderer.FillRect(
            new FloatRect(
                new Vector2f(Bounds.Left + 3f, Bounds.Top + 3f),
                new Vector2f(Bounds.Width, Bounds.Height)),
            Theme.Shade);

        renderer.FillAndStroke(Bounds, Theme.DialogFace, Theme.LineStrong);

        var y = Bounds.Top + PaddingY;

        for (var index = 0; index < _items.Count; index++)
        {
            var item = _items[index];

            if (item.IsSeparator)
            {
                renderer.HorizontalLine(
                    Bounds.Left + 6f,
                    MathF.Round(y + (SeparatorHeight / 2f)),
                    Bounds.Width - 12f,
                    Theme.Line);

                y += SeparatorHeight;
                continue;
            }

            var row = new FloatRect(new Vector2f(Bounds.Left, y), new Vector2f(Bounds.Width, ItemHeight));

            if (index == _hovered)
            {
                renderer.FillRect(row, Theme.RowHover);
            }

            var textTop = row.Top + MathF.Round((ItemHeight - Theme.FontSizeSmall) / 2f) - 1f;

            renderer.DrawText(
                renderer.Ellipsize(item.Label, row.Width - (PaddingX * 2), Theme.FontSizeSmall),
                row.Left + PaddingX,
                textTop,
                Theme.FontSizeSmall,
                item.Enabled ? Theme.Text : Theme.TextFaint);

            if (item.Shortcut.Length > 0)
            {
                renderer.DrawText(
                    item.Shortcut,
                    row.Left + row.Width - PaddingX,
                    textTop + 1f,
                    Theme.FontSizeLabel,
                    item.Enabled ? Theme.TextFaint : Theme.Dim(Theme.TextFaint, 0.7f),
                    TextFont.Mono,
                    TextAlign.Right);
            }

            y += ItemHeight;
        }
    }

    private int IndexAt(Vector2f point)
    {
        if (!Bounds.Contains(point))
        {
            return -1;
        }

        var y = Bounds.Top + PaddingY;

        for (var index = 0; index < _items.Count; index++)
        {
            var item = _items[index];
            var height = item.IsSeparator ? SeparatorHeight : ItemHeight;

            if (point.Y >= y && point.Y < y + height)
            {
                return item.IsSeparator || !item.Enabled ? -1 : index;
            }

            y += height;
        }

        return -1;
    }
}
