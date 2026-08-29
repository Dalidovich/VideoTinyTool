using SFML.Graphics;
using SFML.System;

namespace VideoTinyTool.Ui.Widgets;

public sealed class Slider
{
    private const float KnobRadius = 5f;

    private bool _dragging;

    public FloatRect Bounds { get; set; }

    public float Value { get; set; }

    public float TrackHeight { get; set; } = 4f;

    public Color FillColor { get; set; } = Theme.Accent;

    public bool ShowKnob { get; set; } = true;

    public bool Dragging => _dragging;

    public event Action<float>? ValueChanged;

    public event Action? DragFinished;

    public bool OnMouseDown(Vector2f point)
    {
        var hitArea = new FloatRect(
            new Vector2f(Bounds.Left, Bounds.Top - 6),
            new Vector2f(Bounds.Width, Bounds.Height + 12));

        if (!hitArea.Contains(point))
        {
            return false;
        }

        _dragging = true;
        Apply(point);
        return true;
    }

    public bool OnMouseMove(Vector2f point)
    {
        if (!_dragging)
        {
            return false;
        }

        Apply(point);
        return true;
    }

    public bool OnMouseUp()
    {
        if (!_dragging)
        {
            return false;
        }

        _dragging = false;
        DragFinished?.Invoke();
        return true;
    }

    private void Apply(Vector2f point)
    {
        if (Bounds.Width <= 0)
        {
            return;
        }

        var value = Math.Clamp((point.X - Bounds.Left) / Bounds.Width, 0f, 1f);
        if (Math.Abs(value - Value) < 0.00001f)
        {
            return;
        }

        Value = value;
        ValueChanged?.Invoke(value);
    }

    public void Draw(Renderer renderer)
    {
        var trackTop = Bounds.Top + ((Bounds.Height - TrackHeight) / 2f);
        var track = new FloatRect(new Vector2f(Bounds.Left, trackTop), new Vector2f(Bounds.Width, TrackHeight));

        renderer.FillRect(track, Theme.TrackGroove);
        renderer.FillRect(
            new FloatRect(track.Position, new Vector2f(Bounds.Width * Value, TrackHeight)),
            FillColor);

        if (!ShowKnob)
        {
            return;
        }

        var knobX = Bounds.Left + (Bounds.Width * Value);
        var knobY = trackTop + (TrackHeight / 2f);
        renderer.FillRect(
            new FloatRect(
                new Vector2f(knobX - KnobRadius, knobY - KnobRadius),
                new Vector2f(KnobRadius * 2, KnobRadius * 2)),
            Theme.Accent);
    }
}
