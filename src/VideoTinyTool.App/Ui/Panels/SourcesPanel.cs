using System.Globalization;
using SFML.Graphics;
using SFML.System;
using SFML.Window;
using VideoTinyTool.Domain;
using VideoTinyTool.Ui.Widgets;

namespace VideoTinyTool.Ui.Panels;

public sealed class SourcesPanel : PanelBase
{
    private readonly IEditorHost _host;
    private readonly ScrollableList _list = new();

    private int _hoverIndex = -1;

    public SourcesPanel(IEditorHost host)
    {
        _host = host;
    }

    public override void Draw(Renderer renderer)
    {
        renderer.FillRect(Bounds, Theme.Panel);
        renderer.VerticalLine(Bounds.Left + Bounds.Width - 1, Bounds.Top, Bounds.Height, Theme.Line);

        var count = _host.Sources.Count;
        DrawHeader(renderer, "Sources", count == 1 ? "1 file" : $"{count} files");

        _list.Bounds = BodyBounds;
        _list.ItemCount = count;
        _list.Clamp();

        renderer.PushClip(_list.Bounds);

        if (count == 0)
        {
            renderer.DrawText(
                "No sources yet — use Import…",
                _list.Bounds.Left + Theme.Padding,
                _list.Bounds.Top + Theme.Padding,
                Theme.FontSizeSmall,
                Theme.TextFaint);
        }

        foreach (var index in _list.VisibleIndices())
        {
            DrawRow(renderer, index, _list.RowBounds(index));
        }

        _list.DrawScrollBar(renderer);
        renderer.PopClip();
    }

    private void DrawRow(Renderer renderer, int index, FloatRect row)
    {
        var source = _host.Sources[index];
        var selected = _host.SelectedSource is not null && _host.SelectedSource.Id == source.Id;
        var missing = _host.SourceMissing(source.Id);

        if (selected)
        {
            renderer.FillRect(row, Theme.RowSelected);
            renderer.FillRect(row.Left, row.Top, 2, row.Height, Theme.Accent);
        }
        else if (_hoverIndex == index)
        {
            renderer.FillRect(row, Theme.RowHover);
        }

        renderer.HorizontalLine(row.Left, row.Top + row.Height - 1, row.Width, Theme.Line);

        var thumbBounds = new FloatRect(
            new Vector2f(row.Left + Theme.Padding, row.Top + 7),
            new Vector2f(Theme.SourceThumbWidth, Theme.SourceThumbHeight));

        var texture = _host.Thumbnails.GetPoster(source);
        if (texture is not null)
        {
            renderer.DrawTextureCover(texture, thumbBounds);
        }
        else
        {
            renderer.FillRect(thumbBounds, Theme.ClipFace);
        }

        var textLeft = thumbBounds.Left + thumbBounds.Width + 9;
        var textWidth = row.Left + row.Width - textLeft - Theme.Padding - 6;

        renderer.DrawText(
            renderer.Ellipsize(source.FileName, textWidth, Theme.FontSizeBody),
            textLeft,
            row.Top + 8,
            Theme.FontSizeBody,
            missing ? Theme.ClipMissingBorder : Theme.Text);

        var meta = string.Format(
            CultureInfo.InvariantCulture,
            "{0} · {1}×{2} · {3}",
            TimeFormat.Timecode(source.Duration),
            source.Width,
            source.Height,
            TimeFormat.FrameRate(source.FrameRate));

        if (missing)
        {
            meta = "file is missing";
        }
        else if (!source.HasAudio)
        {
            meta += " · no audio";
        }

        renderer.DrawText(
            renderer.Ellipsize(meta, textWidth, Theme.FontSizeLabel, TextFont.Mono),
            textLeft,
            row.Top + 26,
            Theme.FontSizeLabel,
            missing ? Theme.ClipMissingBorder : Theme.TextFaint,
            TextFont.Mono);
    }

    public override void OnMouseMove(Vector2f point) => _hoverIndex = _list.IndexAt(point);

    public override void OnMouseLeave() => _hoverIndex = -1;

    public override void OnMouseDown(Vector2f point, Mouse.Button button, bool doubleClick)
    {
        if (button != Mouse.Button.Left)
        {
            return;
        }

        var index = _list.IndexAt(point);
        if (index < 0)
        {
            return;
        }

        var source = _host.Sources[index];
        _host.SelectSource(source);

        if (doubleClick)
        {
            _host.AppendSourceToTimeline(source);
        }
    }

    public override void OnScroll(Vector2f point, float delta, bool control) => _list.Scroll(delta);

    public MediaSource? SourceAt(Vector2f point)
    {
        var index = _list.IndexAt(point);
        return index >= 0 ? _host.Sources[index] : null;
    }
}
