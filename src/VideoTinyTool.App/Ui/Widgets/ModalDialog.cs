using SFML.Graphics;
using SFML.System;

namespace VideoTinyTool.Ui.Widgets;

public class ModalDialog
{
    private const float Width = 520f;
    private const float HorizontalPadding = 22f;
    private const float VerticalPadding = 18f;
    private const float ButtonHeight = 26f;

    private readonly List<Button> _buttons = new();

    public ModalDialog(string title, string message)
    {
        Title = title;
        Message = message;
    }

    public string Title { get; set; }

    public string Message { get; set; }

    public bool Closed { get; private set; }

    public IReadOnlyList<Button> Buttons => _buttons;

    public FloatRect Bounds { get; private set; }

    public Button AddButton(string label, ButtonStyle style, Action onClick)
    {
        var button = new Button(label, style);
        button.Clicked += onClick;
        _buttons.Add(button);
        return button;
    }

    public void Close() => Closed = true;

    public virtual float ContentHeight(Renderer renderer, float contentWidth) =>
        WrapLines(renderer, contentWidth).Count * 19f;

    public void Layout(Renderer renderer, uint windowWidth, uint windowHeight)
    {
        var contentWidth = Width - (HorizontalPadding * 2);
        var height = VerticalPadding
                     + 22f
                     + 10f
                     + ContentHeight(renderer, contentWidth)
                     + 18f
                     + ButtonHeight
                     + VerticalPadding;

        Bounds = new FloatRect(
            new Vector2f(MathF.Round((windowWidth - Width) / 2f), MathF.Round((windowHeight - height) / 2f)),
            new Vector2f(Width, height));

        var x = Bounds.Left + Bounds.Width - HorizontalPadding;
        var y = Bounds.Top + Bounds.Height - VerticalPadding - ButtonHeight;

        for (var i = _buttons.Count - 1; i >= 0; i--)
        {
            var button = _buttons[i];
            var width = Math.Max(78f, button.PreferredWidth(renderer));
            x -= width;
            button.Bounds = new FloatRect(new Vector2f(x, y), new Vector2f(width, ButtonHeight));
            x -= 8f;
        }
    }

    public void UpdateHover(Vector2f point)
    {
        foreach (var button in _buttons)
        {
            button.UpdateHover(point);
        }
    }

    public virtual void OnMouseDown(Vector2f point)
    {
        foreach (var button in _buttons)
        {
            button.OnMouseDown(point);
        }
    }

    public virtual void OnMouseUp(Vector2f point)
    {
        foreach (var button in _buttons.ToArray())
        {
            button.OnMouseUp(point);
        }
    }

    public virtual void Draw(Renderer renderer, uint windowWidth, uint windowHeight)
    {
        renderer.FillRect(
            new FloatRect(new Vector2f(0, 0), new Vector2f(windowWidth, windowHeight)),
            Theme.Shade);

        renderer.FillAndStroke(Bounds, Theme.DialogFace, Theme.LineStrong);

        var contentLeft = Bounds.Left + HorizontalPadding;
        var contentWidth = Bounds.Width - (HorizontalPadding * 2);

        renderer.DrawText(Title, contentLeft, Bounds.Top + VerticalPadding, 15, Theme.Text, TextFont.SemiBold);

        var y = Bounds.Top + VerticalPadding + 32f;
        DrawContent(renderer, contentLeft, y, contentWidth);

        foreach (var button in _buttons)
        {
            button.Draw(renderer);
        }
    }

    protected virtual void DrawContent(Renderer renderer, float left, float top, float width)
    {
        foreach (var line in WrapLines(renderer, width))
        {
            renderer.DrawText(line, left, top, Theme.FontSizeBody, Theme.TextDim);
            top += 19f;
        }
    }

    protected List<string> WrapLines(Renderer renderer, float width)
    {
        var lines = new List<string>();

        foreach (var paragraph in Message.Replace("\r\n", "\n").Split('\n'))
        {
            if (paragraph.Length == 0)
            {
                lines.Add(string.Empty);
                continue;
            }

            var current = string.Empty;
            foreach (var word in paragraph.Split(' '))
            {
                var candidate = current.Length == 0 ? word : current + " " + word;
                if (renderer.MeasureText(candidate, Theme.FontSizeBody) <= width)
                {
                    current = candidate;
                    continue;
                }

                if (current.Length > 0)
                {
                    lines.Add(current);
                }

                current = renderer.MeasureText(word, Theme.FontSizeBody) <= width
                    ? word
                    : renderer.Ellipsize(word, width, Theme.FontSizeBody);
            }

            lines.Add(current);
        }

        return lines;
    }
}
