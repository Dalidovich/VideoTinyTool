using SFML.Graphics;
using SFML.System;

namespace VideoTinyTool.Ui;

public enum TextFont
{
    Regular,
    SemiBold,
    Mono
}

public enum TextAlign
{
    Left,
    Center,
    Right
}

public sealed class Renderer : IDisposable
{
    private readonly RenderWindow _window;
    private readonly FontCatalog _fonts;
    private readonly RectangleShape _rect = new();
    private readonly Text _regular;
    private readonly Text _semiBold;
    private readonly Text _mono;
    private readonly Sprite _sprite;
    private readonly Stack<FloatRect> _clipStack = new();
    private View _baseView;
    private readonly View _clipView = new();

    public Renderer(RenderWindow window, FontCatalog fonts)
    {
        _window = window;
        _fonts = fonts;
        _regular = new Text(fonts.Regular, string.Empty);
        _semiBold = new Text(fonts.SemiBold, string.Empty);
        _mono = new Text(fonts.Mono, string.Empty);
        _sprite = new Sprite(new Texture(new Vector2u(1, 1)));
        _baseView = new View(new FloatRect(new Vector2f(0, 0), new Vector2f(window.Size.X, window.Size.Y)));
        _window.SetView(_baseView);
    }

    public void SetWindowSize(uint width, uint height)
    {
        _clipStack.Clear();
        _baseView.Dispose();
        _baseView = new View(new FloatRect(new Vector2f(0, 0), new Vector2f(width, height)));
        _window.SetView(_baseView);
    }

    public void BeginFrame()
    {
        _clipStack.Clear();
        _window.SetView(_baseView);
    }

    public FontCatalog Fonts => _fonts;

    public void FillRect(FloatRect bounds, Color color)
    {
        _rect.Position = new Vector2f(bounds.Left, bounds.Top);
        _rect.Size = new Vector2f(bounds.Width, bounds.Height);
        _rect.FillColor = color;
        _rect.OutlineThickness = 0;
        _window.Draw(_rect);
    }

    public void FillRect(float x, float y, float width, float height, Color color) =>
        FillRect(new FloatRect(new Vector2f(x, y), new Vector2f(width, height)), color);

    public void StrokeRect(FloatRect bounds, Color color, float thickness = 1f)
    {
        _rect.Position = new Vector2f(bounds.Left + thickness / 2f, bounds.Top + thickness / 2f);
        _rect.Size = new Vector2f(bounds.Width - thickness, bounds.Height - thickness);
        _rect.FillColor = Color.Transparent;
        _rect.OutlineColor = color;
        _rect.OutlineThickness = thickness;
        _window.Draw(_rect);
        _rect.OutlineThickness = 0;
    }

    public void FillAndStroke(FloatRect bounds, Color fill, Color stroke, float thickness = 1f)
    {
        FillRect(bounds, fill);
        StrokeRect(bounds, stroke, thickness);
    }

    public void HorizontalLine(float x, float y, float width, Color color) =>
        FillRect(x, y, width, 1, color);

    public void VerticalLine(float x, float y, float height, Color color) =>
        FillRect(x, y, 1, height, color);

    public void DrawTriangle(Vector2f a, Vector2f b, Vector2f c, Color color)
    {
        Span<Vertex> vertices =
        [
            new Vertex(a, color),
            new Vertex(b, color),
            new Vertex(c, color)
        ];

        _window.Draw(vertices.ToArray(), PrimitiveType.Triangles);
    }

    public void DrawTexture(Texture texture, FloatRect bounds, Color? tint = null)
    {
        _sprite.Texture = texture;
        _sprite.TextureRect = new IntRect(new Vector2i(0, 0), new Vector2i((int)texture.Size.X, (int)texture.Size.Y));
        _sprite.Position = new Vector2f(bounds.Left, bounds.Top);
        _sprite.Scale = new Vector2f(
            texture.Size.X == 0 ? 1 : bounds.Width / texture.Size.X,
            texture.Size.Y == 0 ? 1 : bounds.Height / texture.Size.Y);
        _sprite.Color = tint ?? Color.White;
        _window.Draw(_sprite);
    }

    public void DrawTextureCover(Texture texture, FloatRect bounds, Color? tint = null)
    {
        if (texture.Size.X == 0 || texture.Size.Y == 0)
        {
            return;
        }

        var scale = Math.Max(bounds.Width / texture.Size.X, bounds.Height / texture.Size.Y);
        var width = texture.Size.X * scale;
        var height = texture.Size.Y * scale;

        PushClip(bounds);
        DrawTexture(
            texture,
            new FloatRect(
                new Vector2f(bounds.Left + (bounds.Width - width) / 2f, bounds.Top + (bounds.Height - height) / 2f),
                new Vector2f(width, height)),
            tint);
        PopClip();
    }

    public float DrawText(
        string value,
        float x,
        float y,
        uint size,
        Color color,
        TextFont font = TextFont.Regular,
        TextAlign align = TextAlign.Left)
    {
        var text = Pick(font);
        text.DisplayedString = value;
        text.CharacterSize = size;
        text.FillColor = color;

        var bounds = text.GetLocalBounds();
        var left = align switch
        {
            TextAlign.Center => x - bounds.Width / 2f - bounds.Left,
            TextAlign.Right => x - bounds.Width - bounds.Left,
            _ => x
        };

        text.Position = new Vector2f(MathF.Round(left), MathF.Round(y));
        _window.Draw(text);
        return bounds.Width;
    }

    public float DrawTextCentered(
        string value,
        FloatRect bounds,
        uint size,
        Color color,
        TextFont font = TextFont.Regular)
    {
        var text = Pick(font);
        text.DisplayedString = value;
        text.CharacterSize = size;

        var local = text.GetLocalBounds();
        return DrawText(
            value,
            bounds.Left + bounds.Width / 2f,
            bounds.Top + (bounds.Height - local.Height) / 2f - local.Top,
            size,
            color,
            font,
            TextAlign.Center);
    }

    public float MeasureText(string value, uint size, TextFont font = TextFont.Regular)
    {
        var text = Pick(font);
        text.DisplayedString = value;
        text.CharacterSize = size;
        return text.GetLocalBounds().Width;
    }

    public string Ellipsize(string value, float maxWidth, uint size, TextFont font = TextFont.Regular)
    {
        if (MeasureText(value, size, font) <= maxWidth)
        {
            return value;
        }

        var trimmed = value;
        while (trimmed.Length > 1 && MeasureText(trimmed + "…", size, font) > maxWidth)
        {
            trimmed = trimmed[..^1];
        }

        return trimmed + "…";
    }

    public void PushClip(FloatRect bounds)
    {
        var size = _window.Size;
        var normalized = new FloatRect(
            new Vector2f(bounds.Left / size.X, bounds.Top / size.Y),
            new Vector2f(bounds.Width / size.X, bounds.Height / size.Y));

        if (_clipStack.Count > 0)
        {
            normalized = Intersect(_clipStack.Peek(), normalized);
        }

        _clipStack.Push(normalized);
        ApplyClip(normalized);
    }

    public void PopClip()
    {
        if (_clipStack.Count == 0)
        {
            return;
        }

        _clipStack.Pop();
        if (_clipStack.Count == 0)
        {
            _window.SetView(_baseView);
        }
        else
        {
            ApplyClip(_clipStack.Peek());
        }
    }

    private void ApplyClip(FloatRect normalized)
    {
        _clipView.Center = _baseView.Center;
        _clipView.Size = _baseView.Size;
        _clipView.Viewport = _baseView.Viewport;
        _clipView.Scissor = normalized;
        _window.SetView(_clipView);
    }

    private static FloatRect Intersect(FloatRect a, FloatRect b)
    {
        var left = Math.Max(a.Left, b.Left);
        var top = Math.Max(a.Top, b.Top);
        var right = Math.Min(a.Left + a.Width, b.Left + b.Width);
        var bottom = Math.Min(a.Top + a.Height, b.Top + b.Height);

        return new FloatRect(
            new Vector2f(Math.Clamp(left, 0f, 1f), Math.Clamp(top, 0f, 1f)),
            new Vector2f(Math.Clamp(right - left, 0f, 1f), Math.Clamp(bottom - top, 0f, 1f)));
    }

    private Text Pick(TextFont font) => font switch
    {
        TextFont.SemiBold => _semiBold,
        TextFont.Mono => _mono,
        _ => _regular
    };

    public void Dispose()
    {
        _rect.Dispose();
        _regular.Dispose();
        _semiBold.Dispose();
        _mono.Dispose();
        _sprite.Texture.Dispose();
        _sprite.Dispose();
        _clipView.Dispose();
        _baseView.Dispose();
    }
}
