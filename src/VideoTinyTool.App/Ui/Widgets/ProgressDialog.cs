using System.Globalization;
using SFML.Graphics;
using SFML.System;

namespace VideoTinyTool.Ui.Widgets;

public sealed class ProgressDialog : ModalDialog
{
    private const float BarHeight = 8f;

    public ProgressDialog(string title, string message) : base(title, message)
    {
    }

    public double Progress { get; set; }

    public override float ContentHeight(Renderer renderer, float contentWidth) =>
        base.ContentHeight(renderer, contentWidth) + BarHeight + 26f;

    protected override void DrawContent(Renderer renderer, float left, float top, float width)
    {
        base.DrawContent(renderer, left, top, width);

        var lines = WrapLines(renderer, width).Count;
        var barTop = top + (lines * 19f) + 12f;

        renderer.FillRect(new FloatRect(new Vector2f(left, barTop), new Vector2f(width, BarHeight)), Theme.TrackGroove);
        renderer.FillRect(
            new FloatRect(
                new Vector2f(left, barTop),
                new Vector2f((float)(width * Math.Clamp(Progress, 0, 1)), BarHeight)),
            Theme.Accent);

        renderer.DrawText(
            (Progress * 100).ToString("0", CultureInfo.InvariantCulture) + " %",
            left + width,
            barTop + BarHeight + 6f,
            Theme.FontSizeSmall,
            Theme.TextDim,
            TextFont.Mono,
            TextAlign.Right);
    }
}
