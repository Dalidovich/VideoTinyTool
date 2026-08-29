using SFML.Graphics;
using SFML.System;

namespace VideoTinyTool.Ui;

public readonly record struct EditorLayout(
    FloatRect Toolbar,
    FloatRect Sources,
    FloatRect Preview,
    FloatRect Timeline);

public static class LayoutCalculator
{
    public const int MinimumWindowWidth = 1100;
    public const int MinimumWindowHeight = 700;

    private const float SourcesFraction = 0.22f;
    private const float SourcesMinWidth = 300f;
    private const float SourcesMaxWidth = 480f;

    private const float TimelineFraction = 0.33f;
    private const float TimelineMinHeight = 180f;
    private const float TimelineMaxHeight = 320f;

    public static EditorLayout Compute(uint windowWidth, uint windowHeight)
    {
        var width = Math.Max(windowWidth, (uint)MinimumWindowWidth);
        var height = Math.Max(windowHeight, (uint)MinimumWindowHeight);

        var toolbar = new FloatRect(new Vector2f(0, 0), new Vector2f(width, Theme.ToolbarHeight));

        var timelineHeight = MathF.Round(Math.Clamp(height * TimelineFraction, TimelineMinHeight, TimelineMaxHeight));
        var topHeight = height - Theme.ToolbarHeight - timelineHeight;

        var sourcesWidth = MathF.Round(Math.Clamp(width * SourcesFraction, SourcesMinWidth, SourcesMaxWidth));
        if (sourcesWidth > width * 0.5f)
        {
            sourcesWidth = MathF.Round(width * 0.5f);
        }

        var sources = new FloatRect(
            new Vector2f(0, Theme.ToolbarHeight),
            new Vector2f(sourcesWidth, topHeight));

        var preview = new FloatRect(
            new Vector2f(sourcesWidth, Theme.ToolbarHeight),
            new Vector2f(width - sourcesWidth, topHeight));

        var timeline = new FloatRect(
            new Vector2f(0, Theme.ToolbarHeight + topHeight),
            new Vector2f(width, timelineHeight));

        return new EditorLayout(toolbar, sources, preview, timeline);
    }
}
