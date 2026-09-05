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
    private readonly Button _frame = new(I18n.Toolbar.Frame);
    private readonly Button _help = new(I18n.Toolbar.Help);
    private readonly Button _language = new(I18n.Language.Badge, ButtonStyle.Ghost);

    private float _separatorX;
    private float _timecodeRight;

    public ToolbarPanel(IEditorHost host)
    {
        _host = host;

        _import.Clicked += host.ImportFiles;
        _split.Clicked += host.SplitAtPlayhead;
        _remove.Clicked += host.RemoveSelectedClip;
        _undo.Clicked += host.Undo;
        _redo.Clicked += host.Redo;
        _export.Clicked += host.ExportTimeline;
        _frame.Clicked += host.ExportFrame;
        _help.Clicked += host.ShowShortcuts;
        _language.Clicked += ShowLanguageMenu;
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
            yield return _frame;
            yield return _export;
            yield return _help;

            if (LanguagePickerVisible)
            {
                yield return _language;
            }
        }
    }

    private bool LanguagePickerVisible => _host.Languages.Count > 1;

    public override void RefreshText()
    {
        _import.Label = I18n.Toolbar.Import;
        _split.Label = I18n.Toolbar.Split;
        _remove.Label = I18n.Toolbar.Remove;
        _undo.Label = I18n.Toolbar.Undo;
        _redo.Label = I18n.Toolbar.Redo;
        _export.Label = I18n.Toolbar.Export;
        _frame.Label = I18n.Toolbar.Frame;
        _help.Label = I18n.Toolbar.Help;
        _language.Label = I18n.Language.Badge;
    }

    private void ShowLanguageMenu()
    {
        var menu = new ContextMenu(new Vector2f(_language.Bounds.Left, _language.Bounds.Top + _language.Bounds.Height + 2f));

        foreach (var option in _host.Languages)
        {
            var code = option.Code;
            menu.Add(option.Name, option.Badge, code != I18n.Language.Code, () => _host.SwitchLanguage(code));
        }

        _host.ShowContextMenu(menu);
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

        var frameWidth = _frame.PreferredWidth(renderer);
        _frame.Bounds = new FloatRect(
            new Vector2f(MathF.Round(_export.Bounds.Left - gap - frameWidth), MathF.Round(top)),
            new Vector2f(frameWidth, height));

        var helpWidth = Math.Max(height, _help.PreferredWidth(renderer));
        _help.Bounds = new FloatRect(
            new Vector2f(MathF.Round(_frame.Bounds.Left - 8f - helpWidth), MathF.Round(top)),
            new Vector2f(helpWidth, height));

        _timecodeRight = _help.Bounds.Left;

        if (!LanguagePickerVisible)
        {
            return;
        }

        var languageWidth = Math.Max(height, _language.PreferredWidth(renderer));
        _language.Bounds = new FloatRect(
            new Vector2f(MathF.Round(_help.Bounds.Left - 6f - languageWidth), MathF.Round(top)),
            new Vector2f(languageWidth, height));

        _timecodeRight = _language.Bounds.Left;
    }

    private void RefreshEnabled()
    {
        _split.Enabled = _host.SelectedClip is not null;
        _remove.Enabled = _host.SelectedClip is not null;
        _undo.Enabled = _host.History.CanUndo;
        _redo.Enabled = _host.History.CanRedo;
        _export.Enabled = _host.Timeline.HasClips;
        _frame.Enabled = _host.Timeline.HasVideoClips;
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
            _timecodeRight - 14f,
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
