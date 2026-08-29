using SFML.Graphics;
using SFML.System;
using SFML.Window;

namespace VideoTinyTool.Ui.Panels;

public abstract class PanelBase
{
    public FloatRect Bounds { get; set; }

    public bool Contains(Vector2f point) => Bounds.Contains(point);

    public abstract void Draw(Renderer renderer);

    public virtual void OnMouseDown(Vector2f point, Mouse.Button button, bool doubleClick)
    {
    }

    public virtual void OnMouseUp(Vector2f point, Mouse.Button button)
    {
    }

    public virtual void OnMouseMove(Vector2f point)
    {
    }

    public virtual void OnScroll(Vector2f point, float delta, bool control)
    {
    }

    public virtual void OnMouseLeave()
    {
    }

    protected void DrawHeader(Renderer renderer, string label, string meta)
    {
        var header = new FloatRect(Bounds.Position, new Vector2f(Bounds.Width, Theme.PanelHeaderHeight));
        renderer.FillRect(header, Theme.Chrome);
        renderer.HorizontalLine(header.Left, header.Top + header.Height - 1, header.Width, Theme.Line);

        renderer.DrawText(
            label.ToUpperInvariant(),
            header.Left + Theme.Padding,
            header.Top + 6,
            Theme.FontSizeLabel,
            Theme.TextDim,
            TextFont.SemiBold);

        if (meta.Length > 0)
        {
            renderer.DrawText(
                meta,
                header.Left + header.Width - Theme.Padding,
                header.Top + 6,
                Theme.FontSizeLabel,
                Theme.TextFaint,
                TextFont.Mono,
                TextAlign.Right);
        }
    }

    protected FloatRect BodyBounds => new(
        new Vector2f(Bounds.Left, Bounds.Top + Theme.PanelHeaderHeight),
        new Vector2f(Bounds.Width, Bounds.Height - Theme.PanelHeaderHeight));
}
