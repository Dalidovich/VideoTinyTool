using VideoTinyTool.Domain;

namespace VideoTinyTool.Ui;

public enum OverlayHandle
{
    None,
    Body,
    TopLeft,
    TopRight,
    BottomLeft,
    BottomRight,
}

public readonly record struct OverlayBox(float Left, float Top, float Width, float Height)
{
    public float Right => Left + Width;

    public float Bottom => Top + Height;
}

public static class OverlayGeometry
{
    public const float HandleSize = 12f;

    private const double FallbackAspect = 16.0 / 9.0;

    public static OverlayBox Project(OverlayTransform transform, float videoWidth, float videoHeight, double aspect)
    {
        var width = videoWidth * transform.Width;

        return new OverlayBox(
            videoWidth * transform.X,
            videoHeight * transform.Y,
            width,
            (float)(width / Safe(aspect)));
    }

    public static OverlayHandle HitTest(OverlayBox box, float x, float y)
    {
        var reach = HandleSize / 2f;

        if (Near(x, y, box.Left, box.Top, reach))
        {
            return OverlayHandle.TopLeft;
        }

        if (Near(x, y, box.Right, box.Top, reach))
        {
            return OverlayHandle.TopRight;
        }

        if (Near(x, y, box.Left, box.Bottom, reach))
        {
            return OverlayHandle.BottomLeft;
        }

        if (Near(x, y, box.Right, box.Bottom, reach))
        {
            return OverlayHandle.BottomRight;
        }

        return x >= box.Left && x <= box.Right && y >= box.Top && y <= box.Bottom
            ? OverlayHandle.Body
            : OverlayHandle.None;
    }

    public static bool IsCorner(OverlayHandle handle) =>
        handle is OverlayHandle.TopLeft or OverlayHandle.TopRight
            or OverlayHandle.BottomLeft or OverlayHandle.BottomRight;

    public static OverlayTransform Move(
        OverlayTransform start,
        float videoWidth,
        float videoHeight,
        float deltaX,
        float deltaY)
    {
        if (videoWidth <= 0 || videoHeight <= 0)
        {
            return start;
        }

        return (start with
        {
            X = start.X + (deltaX / videoWidth),
            Y = start.Y + (deltaY / videoHeight),
        }).Clamped();
    }

    public static OverlayTransform Resize(
        OverlayTransform start,
        OverlayHandle handle,
        float videoWidth,
        float videoHeight,
        double aspect,
        float deltaX,
        float deltaY)
    {
        if (videoWidth <= 0 || videoHeight <= 0 || !IsCorner(handle))
        {
            return start;
        }

        var safeAspect = Safe(aspect);
        var box = Project(start, videoWidth, videoHeight, safeAspect);

        var signX = handle is OverlayHandle.TopRight or OverlayHandle.BottomRight ? 1f : -1f;
        var signY = handle is OverlayHandle.BottomLeft or OverlayHandle.BottomRight ? 1f : -1f;

        var anchorX = signX > 0 ? box.Left : box.Right;
        var anchorY = signY > 0 ? box.Top : box.Bottom;

        var reachX = (signX > 0 ? box.Right : box.Left) + deltaX - anchorX;
        var reachY = (signY > 0 ? box.Bottom : box.Top) + deltaY - anchorY;

        var inverse = 1.0 / safeAspect;
        var size = ((signX * reachX) + (signY * reachY * inverse)) / (1.0 + (inverse * inverse));

        var width = Math.Clamp((float)(size / videoWidth), OverlayTransform.MinWidth, 1f);
        var pixelWidth = width * videoWidth;
        var pixelHeight = (float)(pixelWidth / safeAspect);

        var left = signX > 0 ? anchorX : anchorX - pixelWidth;
        var top = signY > 0 ? anchorY : anchorY - pixelHeight;

        return (start with
        {
            X = left / videoWidth,
            Y = top / videoHeight,
            Width = width,
        }).Clamped();
    }

    private static bool Near(float x, float y, float targetX, float targetY, float reach) =>
        Math.Abs(x - targetX) <= reach && Math.Abs(y - targetY) <= reach;

    private static double Safe(double aspect) => aspect <= 0 ? FallbackAspect : aspect;
}
