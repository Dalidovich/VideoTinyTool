using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace VideoTinyTool.Platform;

[SupportedOSPlatform("windows")]
public static class WindowIcon
{
    private const int ApplicationIconResource = 32512;
    private const uint ImageIcon = 1;
    private const uint LoadShared = 0x8000;
    private const int WmSetIcon = 0x0080;
    private const int IconSmall = 0;
    private const int IconBig = 1;
    private const int SmCxIcon = 11;
    private const int SmCyIcon = 12;
    private const int SmCxSmIcon = 49;
    private const int SmCySmIcon = 50;

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr GetModuleHandle(string? lpModuleName);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr LoadImage(IntPtr hInst, IntPtr name, uint type, int cx, int cy, uint load);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern int GetSystemMetrics(int index);

    public static void Apply(IntPtr window)
    {
        if (window == IntPtr.Zero)
        {
            return;
        }

        var module = GetModuleHandle(null);
        if (module == IntPtr.Zero)
        {
            return;
        }

        Set(window, module, IconBig, GetSystemMetrics(SmCxIcon), GetSystemMetrics(SmCyIcon));
        Set(window, module, IconSmall, GetSystemMetrics(SmCxSmIcon), GetSystemMetrics(SmCySmIcon));
    }

    private static void Set(IntPtr window, IntPtr module, int slot, int width, int height)
    {
        var icon = LoadImage(module, ApplicationIconResource, ImageIcon, width, height, LoadShared);
        if (icon != IntPtr.Zero)
        {
            SendMessage(window, WmSetIcon, slot, icon);
        }
    }
}
