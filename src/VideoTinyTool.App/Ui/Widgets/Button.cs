using SFML.Graphics;
using SFML.System;

namespace VideoTinyTool.Ui.Widgets;

public enum ButtonStyle
{
    Normal,
    Ghost,
    Accent
}

public sealed class Button
{
    public Button(string label, ButtonStyle style = ButtonStyle.Normal)
    {
        Label = label;
        Style = style;
    }

    public string Label { get; set; }

    public ButtonStyle Style { get; set; }

    public FloatRect Bounds { get; set; }

    public bool Enabled { get; set; } = true;

    public bool Hovered { get; private set; }

    public bool Pressed { get; private set; }

    public event Action? Clicked;

    public float PreferredWidth(Renderer renderer, uint size = Theme.FontSizeSmall) =>
        MathF.Round(renderer.MeasureText(Label, size, TextFont.SemiBold)) + 22;

    public void UpdateHover(Vector2f point) => Hovered = Enabled && Bounds.Contains(point);

    public bool OnMouseDown(Vector2f point)
    {
        if (!Enabled || !Bounds.Contains(point))
        {
            return false;
        }

        Pressed = true;
        return true;
    }

    public bool OnMouseUp(Vector2f point)
    {
        var wasPressed = Pressed;
        Pressed = false;

        if (!wasPressed || !Enabled || !Bounds.Contains(point))
        {
            return false;
        }

        Clicked?.Invoke();
        return true;
    }

    public void Draw(Renderer renderer)
    {
        var face = Style switch
        {
            ButtonStyle.Accent => Pressed ? Theme.AccentBorder : Hovered ? Theme.AccentHover : Theme.Accent,
            ButtonStyle.Ghost => Pressed ? Theme.ButtonFace : Hovered ? Theme.RowHover : Color.Transparent,
            _ => Pressed ? Theme.ButtonActive : Hovered ? Theme.ButtonHover : Theme.ButtonFace
        };

        var border = Style switch
        {
            ButtonStyle.Accent => Theme.AccentBorder,
            ButtonStyle.Ghost => Color.Transparent,
            _ => Theme.LineStrong
        };

        var ink = Style switch
        {
            ButtonStyle.Accent => Theme.AccentInk,
            ButtonStyle.Ghost => Theme.TextDim,
            _ => Theme.Text
        };

        if (!Enabled)
        {
            face = Theme.WithAlpha(face, 70);
            border = Theme.WithAlpha(border, 70);
            ink = Theme.TextFaint;
        }

        if (face.A > 0)
        {
            renderer.FillRect(Bounds, face);
        }

        if (border.A > 0)
        {
            renderer.StrokeRect(Bounds, border);
        }

        renderer.DrawTextCentered(Label, Bounds, Theme.FontSizeSmall, ink, TextFont.SemiBold);
    }
}
