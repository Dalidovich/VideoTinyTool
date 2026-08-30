using SFML.Graphics;
using SFML.System;

namespace VideoTinyTool.Ui.Widgets;

public sealed class OptionPicker
{
    private const float StepWidth = 22f;
    private const float StepGap = 4f;

    private readonly IReadOnlyList<string> _options;
    private readonly Button _previous = new("<");
    private readonly Button _next = new(">");

    public OptionPicker(IReadOnlyList<string> options, string current)
    {
        _options = options.Count > 0 ? options : new[] { current };
        Index = Math.Max(0, IndexOf(_options, current));

        _previous.Clicked += () => Step(-1);
        _next.Clicked += () => Step(1);
        _previous.Enabled = _options.Count > 1;
        _next.Enabled = _options.Count > 1;
    }

    public int Index { get; private set; }

    public string Value => _options[Index];

    public FloatRect Bounds { get; set; }

    public void Layout()
    {
        _previous.Bounds = new FloatRect(
            new Vector2f(Bounds.Left, Bounds.Top),
            new Vector2f(StepWidth, Bounds.Height));

        _next.Bounds = new FloatRect(
            new Vector2f(Bounds.Left + Bounds.Width - StepWidth, Bounds.Top),
            new Vector2f(StepWidth, Bounds.Height));
    }

    public void UpdateHover(Vector2f point)
    {
        _previous.UpdateHover(point);
        _next.UpdateHover(point);
    }

    public void OnMouseDown(Vector2f point)
    {
        _previous.OnMouseDown(point);
        _next.OnMouseDown(point);
    }

    public void OnMouseUp(Vector2f point)
    {
        _previous.OnMouseUp(point);
        _next.OnMouseUp(point);
    }

    public void Draw(Renderer renderer)
    {
        var valueBounds = new FloatRect(
            new Vector2f(Bounds.Left + StepWidth + StepGap, Bounds.Top),
            new Vector2f(Bounds.Width - (StepWidth * 2) - (StepGap * 2), Bounds.Height));

        renderer.FillAndStroke(valueBounds, Theme.Sunk, Theme.Line);
        renderer.DrawTextCentered(
            renderer.Ellipsize(Value, valueBounds.Width - 10f, Theme.FontSizeSmall, TextFont.SemiBold),
            valueBounds,
            Theme.FontSizeSmall,
            Theme.Text,
            TextFont.SemiBold);

        _previous.Draw(renderer);
        _next.Draw(renderer);
    }

    private void Step(int delta) => Index = (Index + delta + _options.Count) % _options.Count;

    private static int IndexOf(IReadOnlyList<string> options, string value)
    {
        for (var i = 0; i < options.Count; i++)
        {
            if (string.Equals(options[i], value, StringComparison.OrdinalIgnoreCase))
            {
                return i;
            }
        }

        return -1;
    }
}
