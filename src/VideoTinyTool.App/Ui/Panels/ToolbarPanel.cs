using SFML.Graphics;
using SFML.System;
using SFML.Window;
using VideoTinyTool.Localization;
using VideoTinyTool.Ui.Widgets;

namespace VideoTinyTool.Ui.Panels;

public sealed class ToolbarPanel : PanelBase
{
    private readonly IEditorHost _host;
    private readonly Button _import = new(I18n.Toolbar.Import);
    private readonly Button _split = new(I18n.Toolbar.Split);
    private readonly Button _remove = new(I18n.Toolbar.Remove);
    private readonly Button _undo = new(I18n.Toolbar.Undo, ButtonStyle.Ghost);
    private readonly Button _redo = new(I18n.Toolbar.Redo, ButtonStyle.Ghost);
    private readonly Button _export = new(I18n.Toolbar.Export, ButtonStyle.Accent);
    private readonly Button _help = new(I18n.Toolbar.Help);

    private float _separatorX;

    public ToolbarPanel(IEditorHost host)
    {
        _host = host;

        _import.Clicked += host.ImportFiles;
        _split.Clicked += host.SplitAtPlayhead;
        _remove.Clicked += host.RemoveSelectedClip;
        _undo.Clicked += host.Undo;
        _redo.Clicked += host.Redo;
        _export.Clicked += host.ExportTimeline;
        _help.Clicked += host.ShowShortcuts;
    }

    private IEnumerable<Button> Buttons
    {
        get
        {
            yield return _import;
            yield return _split;
            yield return _remove;
            yield return _undo;
            yield return _redo;
            yield return _export;
            yield return _help;
        }
    }

    public void Layout(Renderer renderer)
    {
        const float gap = 5f;
        const float height = 24f;

        var top = Bounds.Top + ((Bounds.Height - height) / 2f);
        var x = Bounds.Left + 12f + renderer.MeasureText(I18n.Brand.Full, Theme.FontSizeBrand, TextFont.SemiBold) + 18f;

        foreach (var button in new[] { _import, _split, _remove })
        {
            var width = button.PreferredWidth(renderer);
            button.Bounds = new FloatRect(new Vector2f(MathF.Round(x), MathF.Round(top)), new Vector2f(width, height));
            x += width + gap;
        }

        _separatorX = MathF.Round(x + 4f);
        x += 13f;

        foreach (var button in new[] { _undo, _redo })
        {
            var width = button.PreferredWidth(renderer);
            button.Bounds = new FloatRect(new Vector2f(MathF.Round(x), MathF.Round(top)), new Vector2f(width, height));
            x += width + gap;
        }

        var exportWidth = _export.PreferredWidth(renderer) + 6f;
        _export.Bounds = new FloatRect(
            new Vector2f(MathF.Round(Bounds.Left + Bounds.Width - 12f - exportWidth), MathF.Round(top)),
            new Vector2f(exportWidth, height));

        var helpWidth = Math.Max(height, _help.PreferredWidth(renderer));
        _help.Bounds = new FloatRect(
            new Vector2f(MathF.Round(_export.Bounds.Left - 8f - helpWidth), MathF.Round(top)),
            new Vector2f(helpWidth, height));
    }

    private void RefreshEnabled()
    {
        _split.Enabled = _host.SelectedClip is not null;
        _remove.Enabled = _host.SelectedClip is not null;
        _undo.Enabled = _host.History.CanUndo;
        _redo.Enabled = _host.History.CanRedo;
        _export.Enabled = _host.Timeline.Clips.Count > 0;
    }

    public override void Draw(Renderer renderer)
    {
        RefreshEnabled();

        renderer.FillRect(Bounds, Theme.Chrome);
        renderer.HorizontalLine(Bounds.Left, Bounds.Top + Bounds.Height - 1, Bounds.Width, Theme.Line);

        var brandY = Bounds.Top + 10f;
        var x = Bounds.Left + 12f;
        x += renderer.DrawText(I18n.Brand.Head, x, brandY, Theme.FontSizeBrand, Theme.Text, TextFont.SemiBold);
        x += renderer.DrawText(I18n.Brand.Accent, x, brandY, Theme.FontSizeBrand, Theme.Accent, TextFont.SemiBold);
        renderer.DrawText(I18n.Brand.Tail, x, brandY, Theme.FontSizeBrand, Theme.Text, TextFont.SemiBold);

        renderer.FillRect(_separatorX, Bounds.Top + 10f, 1, 18f, Theme.LineStrong);

        foreach (var button in Buttons)
        {
            button.Draw(renderer);
        }

        renderer.DrawText(
            TimeFormat.Timecode(_host.Timeline.TotalDuration),
            _help.Bounds.Left - 14f,
            Bounds.Top + 12f,
            Theme.FontSizeSmall,
            Theme.TextDim,
            TextFont.Mono,
            TextAlign.Right);
    }

    public override void OnMouseMove(Vector2f point)
    {
        foreach (var button in Buttons)
        {
            button.UpdateHover(point);
        }
    }

    public override void OnMouseDown(Vector2f point, Mouse.Button button, bool doubleClick)
    {
        if (button != Mouse.Button.Left)
        {
            return;
        }

        foreach (var candidate in Buttons)
        {
            candidate.OnMouseDown(point);
        }
    }

    public override void OnMouseUp(Vector2f point, Mouse.Button button)
    {
        if (button != Mouse.Button.Left)
        {
            return;
        }

        foreach (var candidate in Buttons)
        {
            if (candidate.OnMouseUp(point))
            {
                return;
            }
        }
    }

    public override void OnMouseLeave()
    {
        foreach (var button in Buttons)
        {
            button.UpdateHover(new Vector2f(-1, -1));
        }
    }
}
