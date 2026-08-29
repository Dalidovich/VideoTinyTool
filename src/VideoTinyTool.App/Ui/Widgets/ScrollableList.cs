using SFML.Graphics;
using SFML.System;

namespace VideoTinyTool.Ui.Widgets;

public sealed class ScrollableList
{
    private const float ScrollBarWidth = 6f;

    private float _offset;

    public FloatRect Bounds { get; set; }

    public float RowHeight { get; set; } = Theme.SourceRowHeight;

    public int ItemCount { get; set; }

    public float Offset => _offset;

    public float ContentHeight => ItemCount * RowHeight;

    public float MaxOffset => Math.Max(0, ContentHeight - Bounds.Height);

    public void Scroll(float delta)
    {
        _offset = Math.Clamp(_offset - (delta * RowHeight), 0, MaxOffset);
    }

    public void Clamp() => _offset = Math.Clamp(_offset, 0, MaxOffset);

    public int IndexAt(Vector2f point)
    {
        if (!Bounds.Contains(point))
        {
            return -1;
        }

        var index = (int)((point.Y - Bounds.Top + _offset) / RowHeight);
        return index >= 0 && index < ItemCount ? index : -1;
    }

    public FloatRect RowBounds(int index) => new(
        new Vector2f(Bounds.Left, Bounds.Top + (index * RowHeight) - _offset),
        new Vector2f(Bounds.Width, RowHeight));

    public IEnumerable<int> VisibleIndices()
    {
        if (ItemCount == 0)
        {
            yield break;
        }

        var first = Math.Max(0, (int)(_offset / RowHeight));
        var last = Math.Min(ItemCount - 1, (int)((_offset + Bounds.Height) / RowHeight));

        for (var i = first; i <= last; i++)
        {
            yield return i;
        }
    }

    public void DrawScrollBar(Renderer renderer)
    {
        if (MaxOffset <= 0)
        {
            return;
        }

        var trackHeight = Bounds.Height;
        var thumbHeight = Math.Max(24f, trackHeight * (Bounds.Height / ContentHeight));
        var travel = trackHeight - thumbHeight;
        var thumbTop = Bounds.Top + (travel * (_offset / MaxOffset));

        renderer.FillRect(
            new FloatRect(
                new Vector2f(Bounds.Left + Bounds.Width - ScrollBarWidth, thumbTop),
                new Vector2f(ScrollBarWidth - 2, thumbHeight)),
            Theme.LineStrong);
    }
}
