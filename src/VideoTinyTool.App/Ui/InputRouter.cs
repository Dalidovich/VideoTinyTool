using SFML.System;
using SFML.Window;
using VideoTinyTool.Ui.Panels;

namespace VideoTinyTool.Ui;

public sealed class InputRouter
{
    private static readonly TimeSpan DoubleClickWindow = TimeSpan.FromMilliseconds(380);
    private const float DoubleClickSlack = 5f;

    private readonly List<PanelBase> _panels = new();

    private PanelBase? _captured;
    private PanelBase? _hovered;
    private DateTime _lastClickAt = DateTime.MinValue;
    private Vector2f _lastClickAtPoint;

    public void Register(PanelBase panel) => _panels.Add(panel);

    public Vector2f MousePosition { get; private set; }

    public void MouseDown(Vector2f point, Mouse.Button button)
    {
        MousePosition = point;

        var panel = PanelAt(point);
        if (panel is null)
        {
            return;
        }

        var now = DateTime.UtcNow;
        var doubleClick = button == Mouse.Button.Left
                          && now - _lastClickAt <= DoubleClickWindow
                          && Math.Abs(point.X - _lastClickAtPoint.X) <= DoubleClickSlack
                          && Math.Abs(point.Y - _lastClickAtPoint.Y) <= DoubleClickSlack;

        _lastClickAt = doubleClick ? DateTime.MinValue : now;
        _lastClickAtPoint = point;

        _captured = panel;
        panel.OnMouseDown(point, button, doubleClick);
    }

    public void MouseUp(Vector2f point, Mouse.Button button)
    {
        MousePosition = point;

        var target = _captured ?? PanelAt(point);
        _captured = null;
        target?.OnMouseUp(point, button);
    }

    public void MouseMove(Vector2f point)
    {
        MousePosition = point;

        if (_captured is not null)
        {
            _captured.OnMouseMove(point);
            return;
        }

        var panel = PanelAt(point);
        if (!ReferenceEquals(panel, _hovered))
        {
            _hovered?.OnMouseLeave();
            _hovered = panel;
        }

        panel?.OnMouseMove(point);
    }

    public void Scroll(Vector2f point, float delta, bool control)
    {
        MousePosition = point;
        PanelAt(point)?.OnScroll(point, delta, control);
    }

    public void ReleaseCapture()
    {
        _captured = null;
        _hovered?.OnMouseLeave();
        _hovered = null;
    }

    private PanelBase? PanelAt(Vector2f point)
    {
        foreach (var panel in _panels)
        {
            if (panel.Contains(point))
            {
                return panel;
            }
        }

        return null;
    }
}
